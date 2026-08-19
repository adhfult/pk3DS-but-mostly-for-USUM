using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

// The enclosing pk3DS.Core.Modding namespace also declares a MechanicType, and namespace members
// win over global aliases - so this alias has to sit inside the namespace to take effect.
using MechanicType = pk3DS.Core.CTR.MechanicType;

/// <summary>Severity of a line in an install plan.</summary>
public enum PlanSeverity { Info = 0, Warning = 1, Error = 2 }

/// <summary>One resolved decision or action in an install plan.</summary>
public sealed class PlanStep
{
    public PlanSeverity Severity { get; init; } = PlanSeverity.Info;
    public string Stage { get; init; } = "";
    public string Message { get; init; } = "";
    public override string ToString() =>
        Severity == PlanSeverity.Info ? $"[{Stage}] {Message}" : $"[{Stage}] {Severity.ToString().ToUpperInvariant()}: {Message}";
}

/// <summary>
/// The full result of planning (and optionally performing) an install.
/// <para>
/// Planning is always done in full before anything is written, so a definition that would fail
/// halfway — no space, unknown mechanic, un-assemblable code — is rejected with a complete
/// explanation instead of leaving a half-patched ROM behind.
/// </para>
/// </summary>
public sealed class InstallPlan
{
    public CustomFunctionDefinition Definition { get; init; }
    public List<PlanStep> Steps { get; } = [];

    public int ResolvedMechanicIndex { get; set; } = -1;
    /// <summary>Game id of the resolved target. This, not a list position, is what Commit acts on.</summary>
    public uint ResolvedId { get; set; }
    /// <summary>The map the plan was resolved against; Commit needs the same view of the ROM.</summary>
    public BattleMechanicMap Map { get; set; }
    /// <summary>Region new code goes into, and the first free byte in it.</summary>
    public (uint Offset, int Length) Reserve { get; set; }
    public uint Bump { get; set; }

    public byte[] Code { get; set; }
    public uint CodeOffset { get; set; }
    public List<BSSSlot> Storage { get; } = [];
    public bool Committed { get; private set; }

    public bool HasErrors => Steps.Any(s => s.Severity == PlanSeverity.Error);
    public IEnumerable<PlanStep> Errors => Steps.Where(s => s.Severity == PlanSeverity.Error);

    internal void Info(string stage, string msg) => Steps.Add(new PlanStep { Stage = stage, Message = msg });
    internal void Warn(string stage, string msg) => Steps.Add(new PlanStep { Stage = stage, Message = msg, Severity = PlanSeverity.Warning });
    internal void Error(string stage, string msg) => Steps.Add(new PlanStep { Stage = stage, Message = msg, Severity = PlanSeverity.Error });
    internal void MarkCommitted() => Committed = true;

    public string Describe() => string.Join(Environment.NewLine, Steps.Select(s => s.ToString()));
}

/// <summary>
/// Installs a <see cref="CustomFunctionDefinition"/> into a decompiled CRO.
/// <para>
/// Split deliberately into <see cref="Plan"/> and <see cref="Commit"/>. Planning resolves every
/// symbolic reference (mechanic name to index, timing to meaning, assembly to bytes, storage to
/// BSS offsets, code to a concrete address) and reports exactly what it intends to do without
/// touching the ROM. Only a plan with no errors can be committed.
/// </para>
/// </summary>
public static class CustomFunctionInstaller
{
    /// <summary>Bytes per relocation entry in a CRO patch table.</summary>
    private const int RelocationEntrySize = 12;

    /// <summary>
    /// How many more relocation entries fit before the table runs into the next segment.
    /// </summary>
    public static int RelocationHeadroom(DecompiledCRO cro)
    {
        if (cro?.Header == null || cro.Segments == null) return int.MaxValue;

        uint start = cro.Header.PatchTableOffset;
        if (start == 0) return int.MaxValue;
        uint end = start + (cro.Header.PatchTableCount * RelocationEntrySize);

        uint ceiling = uint.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            var seg = cro.Segments[i];
            if (seg == null || seg.Size == 0 || i == 3) continue;   // BSS is not file-backed
            if (seg.Start <= start) continue;                        // sits below the table
            if (seg.Start < ceiling) ceiling = seg.Start;
        }

        if (ceiling == uint.MaxValue) return int.MaxValue;
        long spare = ((long)ceiling - end) / RelocationEntrySize;
        return spare > int.MaxValue ? int.MaxValue : (int)spare;
    }

    /// <summary>
    /// Resolves a definition against a ROM and reports what installing it would do. Never mutates
    /// <paramref name="cro"/>'s bytes; BSS/segment bookkeeping happens only in <see cref="Commit"/>.
    /// </summary>
    /// <param name="map">
    /// Optional resolved view of the ROM. When supplied, the plan reports how the chosen timing
    /// byte is already used elsewhere — far more useful than a nominal "meaning", since the timing
    /// vocabulary is mostly undocumented and the reliable way to pick one is to copy a mechanic
    /// that already fires when you want yours to.
    /// </param>
    public static InstallPlan Plan(
        CustomFunctionDefinition def,
        DecompiledCRO cro,
        ResearchDatabase db,
        string[] mechanicNames = null,
        BattleMechanicMap map = null)
    {
        var plan = new InstallPlan { Definition = def };

        if (def == null) { plan.Error("input", "definition is null"); return plan; }
        if (cro == null) { plan.Error("input", "no decompiled CRO supplied"); return plan; }
        plan.Info("definition", $"'{def.Name}' -> {def.Target}, {def.Mechanic}, timing 0x{def.Timing:X2}");

        if (map != null)
        {
            var sameKind = map.OfKind(def.Mechanic)
                .Where(m => m.Slots.Any(s => s.Timing == def.Timing))
                .Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct().Take(4).ToList();
            int total = map.TimingHistogram().GetValueOrDefault(def.Timing);

            if (total == 0)
                plan.Warn("timing", $"0x{def.Timing:X2} is not used by any mechanic in this ROM - verify it fires when you expect");
            else if (sameKind.Count > 0)
                plan.Info("timing", $"0x{def.Timing:X2} used {total}x overall; on {def.Mechanic}s: {string.Join(", ", sameKind)}");
            else
                plan.Warn("timing", $"0x{def.Timing:X2} is used {total}x but never by a {def.Mechanic} - confirm it applies here");
        }
        else
        {
            plan.Info("timing", $"0x{def.Timing:X2}");
        }

        // --- Mechanic table + index ---
        var type = def.Mechanic switch
        {
            CustomMechanicKind.Move => MechanicType.Move,
            CustomMechanicKind.Ability => MechanicType.Ability,
            _ => MechanicType.Item,
        };

        int existingSlots;
        if (map != null)
        {
            MappedMechanic mech;
            if (def.MechanicIndex >= 0)
            {
                mech = map.OfKind(def.Mechanic).FirstOrDefault(m => m.Id == (uint)def.MechanicIndex);
                if (mech == null)
                {
                    var ids = map.OfKind(def.Mechanic).Select(m => (int)m.Id).ToList();
                    string range = ids.Count > 0 ? $"present ids run {ids.Min()}-{ids.Max()}" : "no entries are mapped";
                    plan.Error("mechanic",
                        $"no {def.Mechanic} with id {def.MechanicIndex} is in the located table " +
                        $"({map.OfKind(def.Mechanic).Count()} entries; {range}). " +
                        "Ids without a handler cannot carry a timed function.");
                    return plan;
                }
            }
            else
            {
                var matches = map.OfKind(def.Mechanic)
                    .Where(m => !string.IsNullOrEmpty(m.Name) &&
                                m.Name.Equals(def.MechanicName ?? "", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                {
                    plan.Error("mechanic", $"no {def.Mechanic} named '{def.MechanicName}' in the located table");
                    return plan;
                }
                if (matches.Count > 1)
                {
                    plan.Error("mechanic",
                        $"'{def.MechanicName}' matches {matches.Count} {def.Mechanic} entries " +
                        $"(ids {string.Join(", ", matches.Select(m => $"0x{m.Id:X}"))}); " +
                        "set MechanicIndex, or use a name that identifies one entry");
                    return plan;
                }
                mech = matches[0];
            }

            plan.ResolvedMechanicIndex = map.OfKind(def.Mechanic).ToList().FindIndex(m => m.EntryOffset == mech.EntryOffset);
            plan.ResolvedId = mech.Id;
            plan.Map = map;
            plan.Info("mechanic", $"target {mech}");

            if (!mech.HasTimingTable)
            {
                plan.Error("mechanic",
                    $"{def.Mechanic} '{mech.Name}' resolves to a {mech.Chain?.Shape} rather than a call setup with a timing " +
                    "table, so a timed function cannot be appended to it. Attaching an effect here needs the routine itself " +
                    "to be replaced, or a new call setup created.");
                return plan;
            }
            existingSlots = mech.Slots.Count;
            plan.Info("mechanic", $"existing timing table at 0x{mech.Chain.TimingTableOffset:X} with {existingSlots} entr(ies): " +
                                  string.Join(", ", mech.Slots.Select(s => $"0x{s.Timing:X2}")));
        }
        else
        {
            var table = type switch
            {
                MechanicType.Move => cro.MoveTable,
                MechanicType.Ability => cro.AbilityTable,
                _ => cro.ItemTable,
            };
            if (table == null || table.Entries.Count == 0)
            {
                plan.Error("mechanic",
                    $"the {type} table is not in this CRO. Timed effects attach to Battle.cro; " +
                    "Bag.cro, Shop.cro and code.bin carry hooks rather than an effect table, so " +
                    "use the Code hook tab to branch into your routine from a known address.");
                return plan;
            }

            int index = ResolveMechanicIndex(def, table, mechanicNames, plan);
            if (index < 0) return plan;
            plan.ResolvedMechanicIndex = index;

            var entry = table.Entries[index];
            string label = string.IsNullOrWhiteSpace(entry.Name) ? $"#{index}" : $"#{index} ({entry.Name})";
            plan.Info("mechanic", $"target {type} {label}");
            plan.Warn("mechanic", "planning against CRODecompiler's table discovery, which is known to misidentify these " +
                                  "tables; supply a BattleMechanicMap for a verified target");

            if (entry.CallFunc == null || entry.Timings == null)
            {
                plan.Error("mechanic",
                    $"{type} {label} has no call function / timing table, so a timed function cannot be attached. " +
                    "Adding an effect to a vanilla-empty slot needs the 'new mechanic' path instead.");
                return plan;
            }
            existingSlots = entry.Timings.Entries.Count;
            plan.Info("mechanic", $"existing timing table at 0x{entry.Timings.Offset:X} with {existingSlots} entr(ies)");
        }

        // --- Code ---
        byte[] code = ResolveCode(def, db, plan, cro);
        if (code == null) return plan;
        plan.Code = code;
        plan.Info("code", $"{code.Length} bytes resolved");

        if (code.Length % 4 != 0)
            plan.Warn("code", $"length {code.Length} is not a multiple of 4; ARM expects word-aligned routines");

        // What this routine expects to be handed, against what the stock routines at the same timing
        // take. Reported, never enforced - see RegisterContract.Finding for why it cannot be.
        if (map != null)
        {
            var stock = map.Mechanics
                .SelectMany(m => m.Slots)
                .Where(s => s.Timing == def.Timing)
                .Select(s => RegisterContract.Analyse(cro.RawData, s.FunctionOffset))
                .ToList();

            int spare = RelocationHeadroom(cro);
            if (spare > 0)
                plan.Info("relocations", $"{spare} relocation slot(s) spare before the table overruns a segment");
            else
                plan.Error("relocations",
                    "the relocation table has no room left: adding another entry would overrun the next " +
                    "segment and the CRO would fail to load. Expand the segment (CROUtil.ExpandSegment) " +
                    "or move the table before installing anything more.");

            plan.Info("registers", RegisterContract.EngineAbi);
            foreach (var f in RegisterContract.Compare(RegisterContract.Analyse(code, 0), stock))
                plan.Info("registers", f.Message);
        }

        var allocator = new CodeSpaceAllocator(cro.RawData, db, def.Target);
        plan.Info("space", $"free-space registry: {allocator.RegistryRegions} region(s), {allocator.RegistryRoom} bytes");

        var reserve = CodeRelocator.FindReserve(cro);
        if (reserve.Offset == 0)
        {
            plan.Error("space", "no blank tail in the code segment; run CROUtil.ExpandSegment before installing");
            return plan;
        }
        plan.Reserve = reserve;
        if (plan.Bump < reserve.Offset) plan.Bump = reserve.Offset;
        plan.CodeOffset = plan.Bump;
        plan.Info("space", $"reserve at 0x{reserve.Offset:X6}, {reserve.Length} bytes; first free 0x{plan.Bump:X6}");

        // Both the routine and the enlarged timing table are sized here, so a definition that fits
        // the code but not the table is rejected now rather than midway through writing.
        int newTimingBytes = (existingSlots + 1) * 8;
        int need = ((code.Length + 3) & ~3) + newTimingBytes;
        int available = (int)(reserve.Offset + (uint)reserve.Length - plan.Bump);
        if (need > available)
        {
            plan.Error("space", $"needs {need} bytes ({code.Length} code + {newTimingBytes} timing table), {available} left in the reserve");
            return plan;
        }
        plan.Info("space", $"needs {need} bytes of {available} available");

        // --- Storage ---
        foreach (var req in def.Storage ?? [])
        {
            if (req.Size <= 0) { plan.Error("storage", $"'{req.Name}' has non-positive size {req.Size}"); continue; }
            int total = req.PerPokemon ? req.Size * 24 : req.Size;
            plan.Info("storage", $"'{req.Name}': {total} byte(s) in BSS{(req.PerPokemon ? " (24 battle slots)" : "")}");
        }

        // --- Relocation capacity ---
        plan.Info("relocations", $"{cro.Relocations.Count} existing; installing adds 1 and re-points {existingSlots + 1}");

        plan.Info("plan", plan.HasErrors ? "NOT installable - see errors above" : "ready to commit");
        return plan;
    }

    /// <summary>
    /// Performs an install previously produced by <see cref="Plan"/>. Refuses to act on a plan
    /// carrying errors, and refuses to run twice.
    /// </summary>
    public static bool Commit(InstallPlan plan, DecompiledCRO cro, Action<string> log = null)
    {
        log ??= _ => { };

        if (plan == null) { log("no plan supplied"); return false; }
        if (plan.Committed) { log("plan has already been committed"); return false; }
        if (plan.HasErrors)
        {
            log("refusing to commit a plan with errors:");
            foreach (var e in plan.Errors) log("  " + e);
            return false;
        }
        if (plan.Code == null || plan.ResolvedMechanicIndex < 0) { log("plan is incomplete"); return false; }

        if (plan.Map == null)
        {
            log("this plan was resolved without a BattleMechanicMap, so its target cannot be identified reliably; re-plan with one");
            return false;
        }

        var def = plan.Definition;

        // Storage first: a failure here must happen before any code is written.
        foreach (var req in def.Storage ?? [])
        {
            var slot = MechanicEditor.AllocateBSS(cro, req.Name, req.Size, req.PerPokemon);
            plan.Storage.Add(slot);
            log($"  BSS '{req.Name}' at +0x{slot.Offset:X} ({slot.Size} bytes)");
        }

        uint bump = plan.Bump;
        var outcome = MechanicInstaller.AttachFunction(
            cro, plan.Map, def.Mechanic, plan.ResolvedId,
            new MechanicEffect { Timing = def.Timing, Code = plan.Code, Name = def.Name },
            plan.Reserve, ref bump);

        foreach (var line in outcome.Log) log("  " + line);
        if (!outcome.Success)
        {
            foreach (var e in outcome.Errors) log("  ERROR: " + e);
            log("install failed; nothing was committed beyond any BSS allocation above");
            return false;
        }

        plan.Bump = bump;
        plan.CodeOffset = outcome.Slots.Count > 0 ? outcome.Slots[0].Function : plan.CodeOffset;
        plan.MarkCommitted();
        log($"installed '{def.Name}'");
        return true;
    }

    #region Resolution helpers

    private static int ResolveMechanicIndex(
        CustomFunctionDefinition def, MechanicTable table, string[] names, InstallPlan plan)
    {
        if (def.MechanicIndex >= 0)
        {
            if (def.MechanicIndex >= table.Entries.Count)
            {
                plan.Error("mechanic",
                    $"index {def.MechanicIndex} is outside the {table.Type} table (0..{table.Entries.Count - 1})");
                return -1;
            }
            return def.MechanicIndex;
        }

        if (string.IsNullOrWhiteSpace(def.MechanicName))
        {
            plan.Error("mechanic", "neither MechanicIndex nor MechanicName was supplied");
            return -1;
        }

        // Prefer names already attached to the decompiled entries, then the caller's name table.
        int found = table.Entries.FindIndex(e =>
            !string.IsNullOrWhiteSpace(e.Name) &&
            e.Name.Equals(def.MechanicName, StringComparison.OrdinalIgnoreCase));
        if (found >= 0) return found;

        if (names != null)
        {
            for (int i = 0; i < names.Length && i < table.Entries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(names[i]) &&
                    names[i].Equals(def.MechanicName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        plan.Error("mechanic", $"no {table.Type} named '{def.MechanicName}' was found");
        return -1;
    }

    private static byte[] ResolveCode(CustomFunctionDefinition def, ResearchDatabase db, InstallPlan plan, DecompiledCRO cro)
    {
        if (!string.IsNullOrWhiteSpace(def.ReuseFunctionNamed))
        {
            if (db == null)
            {
                plan.Error("code", "ReuseFunctionNamed needs the research database, which was not supplied");
                return null;
            }
            var matches = db.AllBodies
                .Where(b => b.Target == def.Target && b.Name != null &&
                            b.Name.Contains(def.ReuseFunctionNamed, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(b => b.Size)
                .ToList();

            if (matches.Count == 0)
            {
                plan.Error("code", $"no reusable routine matching '{def.ReuseFunctionNamed}' for {def.Target}");
                return null;
            }
            if (matches.Count > 1)
                plan.Warn("code", $"{matches.Count} routines match '{def.ReuseFunctionNamed}'; using the largest ({matches[0].Name})");

            plan.Info("code", $"reusing routine '{Flatten(matches[0].Name)}' from {matches[0].Origin}");
            return matches[0].Code;
        }

        if (def.Assembly is { Count: > 0 })
        {
            string text = string.Join(Environment.NewLine, def.Assembly);
            if (SymbolSubstitution.HasTokens(text))
            {
                var symbols = db != null ? new ArmSymbolTable(db, def.Target) : null;

                var sub = SymbolSubstitution.Apply(text, symbols, cro?.RawData, db?.Version);

                foreach (string r in sub.Resolved) plan.Info("symbols", r);
                foreach (string e in sub.Errors) plan.Error("symbols", e);
                if (!sub.Success) return null;

                var substituted = def.ResolveCodeFrom(sub.Text, out string subError);
                if (substituted == null) { plan.Error("code", subError); return null; }
                return substituted;
            }
        }

        var bytes = def.ResolveCode(out string error);
        if (bytes == null) { plan.Error("code", error); return null; }
        return bytes;
    }

    private static string Flatten(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var f = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (f.Contains("  ")) f = f.Replace("  ", " ");
        return f.Length <= 90 ? f : f[..87] + "...";
    }

    #endregion
}
