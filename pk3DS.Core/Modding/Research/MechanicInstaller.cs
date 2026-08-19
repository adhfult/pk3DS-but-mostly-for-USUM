using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>One effect to run, and when it runs.</summary>
public sealed class MechanicEffect
{
    /// <summary>Timing byte the battle engine dispatches on.</summary>
    public byte Timing { get; set; }
    /// <summary>Assembled ARM code for the effect. Ignored when <see cref="ExistingFunction"/> is set.</summary>
    public byte[] Code { get; set; } = [];
    /// <summary>
    /// Address of a routine already in the ROM to point at instead of writing new code.
    /// <para>
    /// Several effects are exactly an existing one wearing a different hat — an item that does what
    /// an ability does. Clear Amulet is Clear Body's two routines attached to an item entry, so
    /// pointing at them costs nothing and keeps the behaviour identical by construction, where a
    /// copy would be one more thing to keep in step.
    /// </para>
    /// </summary>
    public uint ExistingFunction { get; set; }
    /// <summary>
    /// Address <see cref="Code"/> was assembled for. When set, the installer rebases the bytes to
    /// wherever it actually places them.
    /// <para>
    /// Rebasing has to happen here, not in the caller. A caller can only guess the final address,
    /// and the guess is wrong as soon as the master table is relocated ahead of the routine — which
    /// is every install. Code rebased for a predicted address and then written somewhere else has
    /// every branch off by the difference: Loaded Dice's two routines ended up calling into each
    /// other instead of their shared helper, which crashes the moment a multi-hit move is used.
    /// </para>
    /// </summary>
    public uint SourceBase { get; set; }

    /// <summary>Label used in logs only.</summary>
    public string Name { get; set; } = "";

    public bool ReusesExisting => ExistingFunction != 0;
}

/// <summary>A brand-new move / ability / item effect to add to a master table.</summary>
public sealed class NewMechanicRequest
{
    public CustomMechanicKind Kind { get; set; } = CustomMechanicKind.Move;
    /// <summary>Game id the new entry answers to (e.g. move 805 for Terrain Pulse).</summary>
    public uint Id { get; set; }
    public string Name { get; set; } = "";
    public List<MechanicEffect> Effects { get; set; } = [];

    /// <summary>
    /// The game's own name list for this kind of mechanic, used to confirm <see cref="Id"/> really
    /// is the thing being installed.
    /// <para>
    /// Supply it whenever you can. Ids picked by reasoning rather than by lookup put Sharpness and
    /// Transistor onto 234 and 235 — which in an expanded build are Intrepid Sword and Dauntless
    /// Shield, not free slots — and nothing about the install looked wrong afterwards, because
    /// structurally it wasn't. The name table is the only thing that catches that class of mistake.
    /// </para>
    /// </summary>
    public string[] NameTable { get; set; }
}

/// <summary>What an install did, in enough detail to audit it afterwards.</summary>
public sealed class MechanicInstallResult
{
    public bool Success { get; set; }
    public List<string> Log { get; } = [];
    public List<string> Errors { get; } = [];

    public uint TableOffset { get; set; }
    public bool TableRelocated { get; set; }
    public int EntryCountBefore { get; set; }
    public int EntryCountAfter { get; set; }
    public uint HandlerOffset { get; set; }
    public uint TimingTableOffset { get; set; }
    public List<(byte Timing, uint Function)> Slots { get; } = [];
    public List<uint> CountSitesPatched { get; } = [];
    public int BytesUsed { get; set; }

    internal void Say(string s) => Log.Add(s);
    internal void Fail(string s) { Errors.Add(s); Success = false; }

    public string Describe()
    {
        var lines = new List<string>(Log);
        lines.AddRange(Errors.Select(e => "ERROR: " + e));
        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Adds and extends battle mechanics in a Battle.cro, growing the master tables when they have no
/// room left.
/// <para>
/// This replaces <see cref="MechanicEditor.AddNewMechanic"/>, which cannot do the job: it writes a
/// master entry as <c>[pointer][reserved]</c> when the real record is <c>[id][handler]</c>, and its
/// table-growth path only bumps an in-memory counter and then writes past the end of the table —
/// straight over whatever data follows. It also depends on <see cref="CRODecompiler"/>'s table
/// discovery, measured to be wrong even on stock cartridges.
/// </para>
/// <para>
/// The tables here are located by id fingerprint (<see cref="BattleMechanicMap"/>), and growth is
/// handled honestly: the move table has live data immediately behind it, so adding an entry means
/// copying the table into the code reserve, repointing every per-entry handler relocation and the
/// pointer that finds the table, then widening the entry-count literal the walker reads.
/// </para>
/// </summary>
public static class MechanicInstaller
{
    /// <summary>Canonical call-setup stub: publish the slot count, hand back the timing table.</summary>
    private const int StubSize = 20;

    /// <summary>
    /// Adds a new entry to a master table, complete with call-setup stub, timing table and effect
    /// functions, relocating the master table first if it cannot grow where it stands.
    /// </summary>
    /// <param name="map">A freshly built map of <paramref name="cro"/>; used to find the table.</param>
    /// <param name="reserve">
    /// Where new code and tables go. Pass <see cref="CodeRelocator.FindTailReserve"/> unless the
    /// caller is managing its own arena.
    /// </param>
    /// <param name="bump">
    /// First free byte in the reserve, advanced past everything written. Pass the previous call's
    /// value to install several effects into one reserve without overlap.
    /// </param>
    public static MechanicInstallResult AddMechanic(
        DecompiledCRO cro,
        BattleMechanicMap map,
        NewMechanicRequest request,
        (uint Offset, int Length) reserve,
        ref uint bump)
    {
        var result = new MechanicInstallResult { Success = true };

        if (cro == null || map == null || request == null) { result.Fail("missing cro, map or request"); return result; }
        if (request.Effects.Count == 0) { result.Fail("no effects supplied; an entry with an empty timing table would do nothing"); return result; }
        if (reserve.Offset == 0 || reserve.Length <= 0) { result.Fail("no code reserve available; expand the CRO first"); return result; }

        var table = map.Tables.FirstOrDefault(t => t.Kind == request.Kind);
        if (table == null) { result.Fail($"no {request.Kind} master table was located in this ROM"); return result; }

        if (map.Find(request.Kind, request.Id) != null)
        { result.Fail($"{request.Kind} id 0x{request.Id:X} already has an entry; use AttachFunction to extend it"); return result; }

        if (request.NameTable != null)
        {
            string named = request.Id < request.NameTable.Length ? (request.NameTable[request.Id] ?? "").Trim() : null;
            if (named == null)
            { result.Fail($"id {request.Id} is past the end of the {request.Kind} name list ({request.NameTable.Length} entries)"); return result; }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                result.Say($"id {request.Id} is '{named}' in this build (no name given to check against)");
                goto nameChecked;
            }

            // Apostrophes differ between the game's text and hand-typed names (Dragon's Maw).
            bool matches = named.Replace('’', '\'').Equals(request.Name?.Replace('’', '\'') ?? "", StringComparison.OrdinalIgnoreCase);
            bool free = named is "" or "???" or "-----" or "—";

            if (matches) result.Say($"id {request.Id} is '{named}' in this build");
            else if (free) result.Say($"id {request.Id} is an unnamed slot ('{named}'); name it in the editor to make it usable");
            else
            {
                result.Fail($"id {request.Id} is '{named}' in this build, not '{request.Name}' - installing here would " +
                            "give that mechanic your effect");
                return result;
            }
        }
        nameChecked:

        int count = table.EntryCount;
        result.TableOffset = table.TableOffset;
        result.EntryCountBefore = count;
        result.Say($"{request.Kind} table @0x{table.TableOffset:X6} x{count} ({count * 8} bytes)");

        if (bump < reserve.Offset) bump = reserve.Offset;
        uint reserveEnd = reserve.Offset + (uint)reserve.Length;
        uint startBump = bump;

        bool terminated = HasTerminator(cro, table.TableOffset, count);
        int trailing = terminated ? 1 : 0;
        if (terminated)
            result.Say($"table is blank-terminated at 0x{table.TableOffset + (uint)(count * 8):X6}; the terminator moves with it");

        uint newTable = Align(bump);
        bump = newTable + (uint)((count + 1 + trailing) * 8);
        result.Say($"relocating the table to 0x{newTable:X6}");

        uint stub = Align(bump);
        bump = stub + StubSize;

        var functionOffsets = new uint[request.Effects.Count];
        for (int i = 0; i < request.Effects.Count; i++)
        {
            var fn = request.Effects[i];
            if (fn.ReusesExisting)
            {
                if (fn.ExistingFunction >= cro.RawData.Length)
                { result.Fail($"effect '{fn.Name}': 0x{fn.ExistingFunction:X6} is outside the binary"); return result; }
                functionOffsets[i] = fn.ExistingFunction;
                continue;
            }
            if (fn.Code == null || fn.Code.Length == 0) { result.Fail($"effect '{fn.Name}' (timing 0x{fn.Timing:X2}) has no code"); return result; }
            functionOffsets[i] = Align(bump);
            bump = functionOffsets[i] + (uint)fn.Code.Length;
        }

        uint timingTable = Align(bump);
        bump = timingTable + (uint)(request.Effects.Count * 8);

        if (bump > reserveEnd)
        {
            result.Fail($"reserve exhausted: needs {bump - startBump} bytes, {reserveEnd - startBump} available " +
                        $"(reserve 0x{reserve.Offset:X6} + {reserve.Length}). Expand the code segment and retry.");
            bump = startBump;
            return result;
        }

        // --- Move the master table, taking every reference with it. ---
        {
            uint oldTable = table.TableOffset;
            var countSites = FindEntryCountSites(cro, oldTable, count);

            var move = CodeRelocator.MoveBlock(cro, oldTable, count * 8, newTable, fixInstructionBranches: false);
            result.Say($"repointed {move.SlotsRepointed} entry relocation(s), {move.TargetsRepointed} table pointer(s)");
            foreach (var w in move.Warnings) result.Say("  ! " + w);

            // The old copy would otherwise sit there as a stale duplicate that still fingerprints as
            // the table, so blank it; nothing points at it any more.
            Array.Clear(cro.RawData, (int)oldTable, count * 8);

            result.TableRelocated = true;

            if (countSites.Count == 0 && !terminated)
            {
                result.Fail($"the {request.Kind} table is neither blank-terminated nor bounded by a count this tool can find, " +
                            "so there is no way to tell the game it got longer; the extra entry would be ignored");
                bump = startBump;
                return result;
            }
            PatchCountSites(cro, countSites, count, count + 1, result);
        }

        result.TableOffset = newTable;

        // --- Call-setup stub. ---
        WriteStub(cro.RawData, stub, request.Effects.Count);
        if (!CodeRelocator.AddPointer(cro, stub + 16, timingTable, result.Say))
        { result.Fail("could not register the stub's timing-table pointer"); return result; }
        result.HandlerOffset = stub;

        // --- Effect functions, rebased to where they are actually going. ---
        for (int i = 0; i < request.Effects.Count; i++)
        {
            var fn = request.Effects[i];
            if (fn.ReusesExisting) continue;

            byte[] bytes = fn.Code;
            if (fn.SourceBase != 0 && fn.SourceBase != functionOffsets[i])
            {
                var (rebased, rep) = CodeRelocator.RebaseCode(fn.Code, fn.SourceBase, functionOffsets[i]);
                bytes = rebased;
                result.Say($"'{fn.Name}' rebased 0x{fn.SourceBase:X6} -> 0x{functionOffsets[i]:X6} " +
                           $"({rep.ExternalBranchesFixed} external, {rep.InternalBranchesKept} internal)");
                foreach (var w in rep.Warnings) result.Say("  ! " + w);
            }
            bytes.CopyTo(cro.RawData, (int)functionOffsets[i]);
        }

        // --- Timing table: [timing][pad x3][function pointer]. ---
        for (int i = 0; i < request.Effects.Count; i++)
        {
            uint entry = timingTable + (uint)(i * 8);
            cro.RawData[entry] = request.Effects[i].Timing;
            cro.RawData[entry + 1] = 0;
            cro.RawData[entry + 2] = 0;
            cro.RawData[entry + 3] = 0;
            if (!CodeRelocator.AddPointer(cro, entry + 4, functionOffsets[i], result.Say))
            { result.Fail($"could not register the pointer for timing 0x{request.Effects[i].Timing:X2}"); return result; }
            result.Slots.Add((request.Effects[i].Timing, functionOffsets[i]));
        }
        result.TimingTableOffset = timingTable;

        // --- Master table entry: [id][handler]. ---
        uint newEntry = newTable + (uint)(count * 8);
        BitConverter.GetBytes(request.Id).CopyTo(cro.RawData, (int)newEntry);
        if (!CodeRelocator.AddPointer(cro, newEntry + 4, stub, result.Say))
        { result.Fail("could not register the master-table handler pointer"); return result; }

        // Re-emit the blank record the walk stops on, now one entry further along.
        if (terminated) Array.Clear(cro.RawData, (int)(newEntry + 8), 8);

        result.EntryCountAfter = count + 1;
        result.BytesUsed = (int)(bump - startBump);
        result.Say($"layout: table 0x{newTable:X6}, stub 0x{stub:X6}, " +
                   string.Join(", ", request.Effects.Select((e, i) => $"fn[0x{e.Timing:X2}] 0x{functionOffsets[i]:X6}")) +
                   $", timings 0x{timingTable:X6}; end 0x{bump:X6}");
        result.Say($"{request.Kind} 0x{request.Id:X} '{request.Name}' -> handler 0x{stub:X6}, {request.Effects.Count} slot(s), {result.BytesUsed} bytes used");
        return result;
    }

    /// <summary>
    /// Adds one more timed effect to a mechanic that already has a call setup, relocating its
    /// timing table into the reserve because tables sit nose-to-tail with no slack.
    /// </summary>
    public static MechanicInstallResult AttachFunction(
        DecompiledCRO cro,
        BattleMechanicMap map,
        CustomMechanicKind kind,
        uint id,
        MechanicEffect effect,
        (uint Offset, int Length) reserve,
        ref uint bump)
    {
        var result = new MechanicInstallResult { Success = true };

        if (cro == null || map == null || effect == null) { result.Fail("missing cro, map or effect"); return result; }
        if (!effect.ReusesExisting && effect.Code is not { Length: > 0 })
        { result.Fail("effect has neither code nor an existing routine to reuse"); return result; }

        var mech = map.Find(kind, id);
        if (mech == null) { result.Fail($"no {kind} with id 0x{id:X} in this ROM"); return result; }
        if (!mech.HasTimingTable)
        {
            result.Fail($"{kind} 0x{id:X} '{mech.Name}' is a {mech.Chain?.Shape} with no timing table; " +
                        "it has to be converted to a call setup before functions can be attached");
            return result;
        }

        int oldCount = mech.Slots.Count;
        if (bump < reserve.Offset) bump = reserve.Offset;
        uint reserveEnd = reserve.Offset + (uint)reserve.Length;
        uint startBump = bump;

        // A borrowed routine already lives somewhere; only new code needs space carved out.
        uint fn;
        if (effect.ReusesExisting) { fn = effect.ExistingFunction; }
        else { fn = Align(bump); bump = fn + (uint)effect.Code.Length; }
        uint newTiming = Align(bump);
        bump = newTiming + (uint)((oldCount + 1) * 8);

        if (bump > reserveEnd)
        {
            result.Fail($"reserve exhausted: needs {bump - startBump} bytes, {reserveEnd - startBump} available");
            bump = startBump;
            return result;
        }

        if (!effect.ReusesExisting) effect.Code.CopyTo(cro.RawData, (int)fn);

        // Move the existing rows so their function pointers keep resolving, then append.
        uint oldTiming = mech.Chain.TimingTableOffset;
        var move = CodeRelocator.MoveBlock(cro, oldTiming, oldCount * 8, newTiming, fixInstructionBranches: false);
        result.Say($"timing table 0x{oldTiming:X6} -> 0x{newTiming:X6}: {move.SlotsRepointed} slot(s) repointed");
        foreach (var w in move.Warnings) result.Say("  ! " + w);
        Array.Clear(cro.RawData, (int)oldTiming, oldCount * 8);

        uint appended = newTiming + (uint)(oldCount * 8);
        cro.RawData[appended] = effect.Timing;
        cro.RawData[appended + 1] = 0;
        cro.RawData[appended + 2] = 0;
        cro.RawData[appended + 3] = 0;
        if (!CodeRelocator.AddPointer(cro, appended + 4, fn, result.Say))
        { result.Fail("could not register the new function pointer"); return result; }

        // The stub still advertises the old row count, and the walker trusts it.
        if (!UpdateStubCount(cro, mech.HandlerOffset, oldCount + 1, map, result))
        { result.Fail($"could not update the slot count in the stub at 0x{mech.HandlerOffset:X6}"); return result; }

        // The stub's pointer literal has to follow the table.
        int ptrIdx = cro.Relocations.FindIndex(r => r.AbsoluteWriteTo(cro.SegmentStarts) == mech.HandlerOffset + 16);
        if (ptrIdx >= 0)
            CodeRelocator.RepointTargetsIn(cro, oldTiming, 4, newTiming, null);

        result.HandlerOffset = mech.HandlerOffset;
        result.TimingTableOffset = newTiming;
        result.EntryCountBefore = oldCount;
        result.EntryCountAfter = oldCount + 1;
        result.Slots.Add((effect.Timing, fn));
        result.BytesUsed = (int)(bump - startBump);
        result.Say($"{kind} 0x{id:X} '{mech.Name}': {oldCount} -> {oldCount + 1} slot(s), timing 0x{effect.Timing:X2} -> 0x{fn:X6}");
        return result;
    }

    /// <summary>
    /// Guarantees the code segment holds a contiguous free run of at least
    /// <paramref name="bytesNeeded"/>, growing it if it does not.
    /// <para>
    /// Call this <em>before</em> decompiling and before any install. Growing a segment shifts
    /// everything after the insertion point, so every offset computed against the old image —
    /// table addresses, hook sites, the reserve itself — is invalid afterwards and the caller must
    /// rebuild its map from the returned bytes.
    /// </para>
    /// <para>
    /// Each install that adds a master-table entry copies the whole table into the reserve, so
    /// consumption is dominated by table size rather than by effect code: a move-table entry costs
    /// ~2.8 KB regardless of how small the effect is. Sizing the growth generously is much cheaper
    /// than discovering a shortfall halfway through a batch.
    /// </para>
    /// </summary>
    /// <returns>True when the binary was grown; <paramref name="rom"/> is replaced in that case.</returns>
    public static bool EnsureReserve(ref byte[] rom, int bytesNeeded, ResearchDatabase db = null, Action<string> log = null)
    {
        log ??= _ => { };
        if (rom == null || bytesNeeded <= 0) return false;

        DecompiledCRO cro;
        try { cro = CRODecompiler.DecompileStructure((byte[])rom.Clone()); }
        catch (Exception ex) { log($"cannot read the CRO structure: {ex.Message}"); return false; }

        var have = CodeRelocator.FindReserve(cro);
        if (have.Length >= bytesNeeded)
        {
            log($"reserve already holds {have.Length} bytes at 0x{have.Offset:X6}; no growth needed");
            return false;
        }

        // Round up so the segment stays page-friendly and one call covers several installs.
        int add = ((bytesNeeded - have.Length) + 0xFFF) & ~0xFFF;
        log($"largest free run is {have.Length} bytes, need {bytesNeeded}; expanding the code segment by {add} (0x{add:X})");

        byte[] grown;
        try { grown = CROUtil.ExpandSegment(rom, 'c', add); }
        catch (Exception ex) { log($"ExpandSegment failed: {ex.Message}"); return false; }

        if (grown == null || grown.Length <= rom.Length) { log("ExpandSegment produced no growth"); return false; }

        if (!StillReadable(grown, db, out string why))
        {
            log($"growth rejected: {why}");
            log("the binary was left unchanged; free space must come from the existing reserve");
            return false;
        }

        rom = grown;
        var after = CodeRelocator.FindReserve(CRODecompiler.DecompileStructure((byte[])rom.Clone()));
        log($"grown to {rom.Length} bytes; largest free run now {after.Length} at 0x{after.Offset:X6}");
        return true;
    }

    /// <summary>
    /// Confirms a rewritten image still resolves its master tables, using the fingerprint map
    /// rather than <see cref="CRODecompiler"/>'s own table discovery — the latter reports no moves
    /// even on an untouched cartridge, so it cannot tell a broken image from a healthy one.
    /// </summary>
    private static bool StillReadable(byte[] rom, ResearchDatabase db, out string why)
    {
        why = null;
        try
        {
            if (db == null)
            {
                var report = CroVerifier.Verify(CRODecompiler.DecompileStructure((byte[])rom.Clone()));
                if (!report.Ok) { why = "relocations no longer reference valid locations"; return false; }
                return true;
            }

            var map = BattleMechanicMap.Build(rom, db);
            if (map.Tables.Count == 0) { why = "no master table could be located afterwards"; return false; }

            var empty = map.Tables.Where(t => t.EntryCount == 0).Select(t => t.Name).ToList();
            if (empty.Count > 0)
            {
                why = $"{string.Join(", ", empty)} lost every entry - the table data and its relocations came apart";
                return false;
            }
            return true;
        }
        catch (Exception ex) { why = $"the result could not be re-read ({ex.Message})"; return false; }
    }

    #region Internals

    private static uint Align(uint v) => (v + 3u) & ~3u;

    /// <summary>
    /// True when the record after the last entry is blank and unrelocated — the marker the walk
    /// stops on for tables that carry no length anywhere.
    /// </summary>
    private static bool HasTerminator(DecompiledCRO cro, uint tableOffset, int count)
    {
        uint at = tableOffset + (uint)(count * 8);
        if (at + 8 > cro.RawData.Length) return false;
        for (uint i = at; i < at + 8; i++) if (cro.RawData[i] != 0) return false;

        uint[] segs = cro.SegmentStarts;
        return !cro.Relocations.Any(r => r.AbsoluteWriteTo(segs) == at + 4);
    }

    /// <summary>Writes the 20-byte call-setup stub. The trailing word is filled by a relocation.</summary>
    private static void WriteStub(byte[] rom, uint at, int slotCount)
    {
        var enc = ARMCodec.EncodeImm8r4((uint)slotCount)
                  ?? throw new ArgumentOutOfRangeException(nameof(slotCount), $"{slotCount} is not an ARM immediate");

        uint mov = 0xE3A01000u | (enc.rot << 8) | enc.imm8; // MOV R1, #slotCount
        BitConverter.GetBytes(mov).CopyTo(rom, (int)at);
        BitConverter.GetBytes(0xE5801000u).CopyTo(rom, (int)at + 4);  // STR R1, [R0]
        BitConverter.GetBytes(0xE59F0000u).CopyTo(rom, (int)at + 8);  // LDR R0, [PC]  -> +0x10
        BitConverter.GetBytes(0xE12FFF1Eu).CopyTo(rom, (int)at + 12); // BX LR
        BitConverter.GetBytes(0u).CopyTo(rom, (int)at + 16);          // timing table pointer
    }

    /// <summary>
    /// Rewrites the slot count a stub publishes. Most stubs carry it as a <c>MOV R1,#n</c>
    /// immediate; the literal-loading variants (Poison Touch, Stall) keep it in the low half-word
    /// of a packed literal whose high half holds unrelated flags, so only that half is touched.
    /// </summary>
    private static bool UpdateStubCount(DecompiledCRO cro, uint handler, int newCount, BattleMechanicMap map, MechanicInstallResult result)
    {
        byte[] rom = cro.RawData;

        for (int w = 0; w < 6; w++)
        {
            uint at = handler + (uint)(w * 4);
            if (at + 4 > rom.Length) break;
            uint word = BitConverter.ToUInt32(rom, (int)at);
            if ((word & 0x0FFFF000) != 0x03A01000) continue; // MOV R1, #imm

            var enc = ARMCodec.EncodeImm8r4((uint)newCount);
            if (enc == null) { result.Say($"{newCount} is not encodable as an ARM immediate"); return false; }
            uint rebuilt = (word & 0xFFFFF000) | (enc.Value.rot << 8) | enc.Value.imm8;
            BitConverter.GetBytes(rebuilt).CopyTo(rom, (int)at);
            result.Say($"stub count at 0x{at:X6}: MOV R1,#{newCount}");
            return true;
        }

        // Literal form: the pool sits after BX LR; the non-relocated word is the packed count.
        for (int w = 3; w < 8; w++)
        {
            uint at = handler + (uint)(w * 4);
            if (at + 4 > rom.Length) break;
            if (map.ResolveSlot(at) != 0) continue; // that one is the table pointer
            uint packed = BitConverter.ToUInt32(rom, (int)at);
            if ((packed & 0xFFFF) == 0 || (packed & 0xFFFF) > 64) continue;
            uint updated = (packed & 0xFFFF0000u) | (uint)(newCount & 0xFFFF);
            BitConverter.GetBytes(updated).CopyTo(rom, (int)at);
            result.Say($"stub count literal at 0x{at:X6}: 0x{packed:X8} -> 0x{updated:X8}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds where the entry count for a master table is written down.
    /// <para>
    /// Searching the whole binary for the number would be hopeless — 343 is a common word — so the
    /// search is anchored on the literal pools that hold the table pointer itself. The routine that
    /// walks the table loads its address and its length from the same pool, so the count is within
    /// a few words of the pointer. Both forms are recognised: a bare data word, and a
    /// <c>CMP Rn,#count</c> bounds check.
    /// </para>
    /// </summary>
    public static List<(uint Offset, bool IsInstruction)> FindEntryCountSites(DecompiledCRO cro, uint tableOffset, int count)
    {
        var sites = new List<(uint, bool)>();
        byte[] rom = cro.RawData;
        uint[] segs = cro.SegmentStarts;

        var anchors = cro.Relocations
            .Where(r => r.AbsoluteTarget(segs) == tableOffset)
            .Select(r => r.AbsoluteWriteTo(segs))
            .Distinct()
            .ToList();

        const int window = 0x200;
        var seen = new HashSet<uint>();

        foreach (uint anchor in anchors)
        {
            uint lo = anchor > window ? anchor - window : 0;
            uint hi = Math.Min((uint)rom.Length - 4, anchor + window);

            for (uint p = lo & ~3u; p <= hi; p += 4)
            {
                if (!seen.Add(p)) continue;
                uint word = BitConverter.ToUInt32(rom, (int)p);

                if (word == (uint)count) { sites.Add((p, false)); continue; }

                // CMP Rn, #imm, unconditional, not against PC.
                if ((word & 0xFFF00000) == 0xE3500000 && ((word >> 16) & 0xF) != 15 &&
                    ARMCodec.DecodeImm8r4((word >> 8) & 0xF, word & 0xFF) == (uint)count)
                    sites.Add((p, true));
            }
        }

        return sites.OrderBy(s => s.Item1).ToList();
    }

    private static void PatchCountSites(
        DecompiledCRO cro, List<(uint Offset, bool IsInstruction)> sites, int oldCount, int newCount, MechanicInstallResult result)
    {
        if (sites.Count == 0)
        {
            result.Say($"no entry-count site found near the table pointer; the walker may size the table another way " +
                       $"(count stayed {oldCount} in the binary)");
            return;
        }

        foreach (var (offset, isInstruction) in sites)
        {
            if (isInstruction)
            {
                var enc = ARMCodec.EncodeImm8r4((uint)newCount);
                if (enc == null) { result.Say($"0x{offset:X6}: {newCount} is not an ARM immediate; left at {oldCount}"); continue; }
                uint word = BitConverter.ToUInt32(cro.RawData, (int)offset);
                uint rebuilt = (word & 0xFFFFF000) | (enc.Value.rot << 8) | enc.Value.imm8;
                BitConverter.GetBytes(rebuilt).CopyTo(cro.RawData, (int)offset);
                result.Say($"count check at 0x{offset:X6}: CMP #{oldCount} -> #{newCount}");
            }
            else
            {
                BitConverter.GetBytes((uint)newCount).CopyTo(cro.RawData, (int)offset);
                result.Say($"count literal at 0x{offset:X6}: {oldCount} -> {newCount}");
            }
            result.CountSitesPatched.Add(offset);
        }
    }

    #endregion
}
