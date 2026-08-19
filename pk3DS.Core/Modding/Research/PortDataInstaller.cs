using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using pk3DS.Core.CTR;

namespace pk3DS.Core.Modding.Research;

/// <summary>An item carried across builds: its name and its full data record.</summary>
public sealed class PortedItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Complete item record, hex.</summary>
    public string Data { get; set; } = "";
    public byte[] Bytes => Convert.FromHexString(Data);
}

/// <summary>Fields of a move that were changed; everything unlisted is left as the target has it.</summary>
public sealed class PortedMove
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? EffectSequence { get; set; }
    public int? Power { get; set; }
    public int? Type { get; set; }
    public int? Category { get; set; }
    public int? HitMin { get; set; }
    public int? HitMax { get; set; }
}

/// <summary>
/// Ports the non-CRO half: item records, move fields, and the code.bin edits.
/// <para>
/// Item and move data are plain GARC contents and move across builds literally, with one caveat
/// each: an item record is only meaningful if the target's item table is at least as long, and the
/// move archive is a "WD" mini archive inside the GARC — writing move records at a flat stride
/// destroys it, because there is an offset table rather than a fixed stride.
/// </para>
/// </summary>
public static class PortDataInstaller
{
    /// <summary>Writes item records and names. Returns the number applied.</summary>
    public static int ApplyItems(GameConfig cfg, IEnumerable<PortedItem> items, bool commit, Action<string> log = null)
    {
        log ??= _ => { };
        var garc = cfg.GetGARCData("item");
        var files = garc.Files;
        int applied = 0;

        foreach (var item in items)
        {
            if (item.Id < 0 || item.Id >= files.Length)
            { log($"  item {item.Id}: outside the target's item table ({files.Length})"); continue; }

            var bytes = item.Bytes;
            if (bytes.Length != files[item.Id].Length)
            { log($"  item {item.Id}: record is {bytes.Length} bytes, target uses {files[item.Id].Length} — skipped"); continue; }

            files[item.Id] = bytes;
            applied++;
        }
        if (commit && applied > 0) garc.Save();
        log($"  item data: {applied} record(s){(commit ? " written" : " (dry run)")}");

        // Names go into every language variant; a hack usually only extends some of them, and the
        // ones it missed will show '???' for anything new.
        int langs = 0;
        for (int lang = 0; lang <= 9; lang++)
        {
            GameConfig lc;
            string[] names;
            try
            {
                lc = new GameConfig(cfg.Version);
                lc.Initialize(cfg.RomFS, cfg.ExeFS, lang);
                names = lc.GetText(TextName.ItemNames);
            }
            catch { continue; }
            if (names.Length == 0) continue;

            bool touched = false;
            foreach (var item in items)
            {
                if (item.Id >= names.Length || string.IsNullOrEmpty(item.Name)) continue;
                string cur = (names[item.Id] ?? "").Trim();
                if (cur == item.Name) continue;
                if (cur is not ("" or "???" or "-----" or "—")) { log($"  lang {lang}: id {item.Id} already named '{cur}' — left alone"); continue; }
                names[item.Id] = item.Name;
                touched = true;
            }
            if (touched && commit) { lc.SetText(TextName.ItemNames, names); lc.SaveText(TextName.ItemNames); }
            if (touched) langs++;
        }
        log($"  item names: {langs} language variant(s){(commit ? " written" : " (dry run)")}");
        return applied;
    }

    /// <summary>Applies the listed move-field changes, repacking the mini archive correctly.</summary>
    public static int ApplyMoves(GameConfig cfg, IEnumerable<PortedMove> moves, bool commit, Action<string> log = null)
    {
        log ??= _ => { };
        var all = cfg.Moves;
        int applied = 0;

        foreach (var pm in moves)
        {
            if (pm.Id < 0 || pm.Id >= all.Length) { log($"  move {pm.Id}: outside the target's move table"); continue; }
            var m = all[pm.Id];
            if (pm.EffectSequence.HasValue) m.EffectSequence = pm.EffectSequence.Value;
            if (pm.Power.HasValue) m.Power = pm.Power.Value;
            if (pm.Type.HasValue) m.Type = pm.Type.Value;
            if (pm.Category.HasValue) m.Category = pm.Category.Value;
            if (pm.HitMin.HasValue) m.HitMin = pm.HitMin.Value;
            if (pm.HitMax.HasValue) m.HitMax = pm.HitMax.Value;
            applied++;
        }

        if (commit && applied > 0)
        {
            var garc = cfg.GetGARCData("move");
            byte[] inner = Mini.PackMini(all.Select(m => m.Write()).ToArray(), "WD");
            var round = Mini.UnpackMini(inner, "WD");
            if (round == null || round.Length != all.Length)
            { log("  move repack did not round-trip — nothing written"); return 0; }
            garc.Files[0] = inner;
            garc.Save();
        }
        log($"  move data: {applied} move(s){(commit ? " written" : " (dry run)")}");
        return applied;
    }

    /// <summary>
    /// Resolves exported symbols to their addresses in the target build, via static.crs.
    /// <para>
    /// This is what makes the code.bin work portable at all: the mint dispatcher and the Ability
    /// Capsule patches call routines like <c>CoreParam::FlipTokuseiIndex</c>, whose address differs
    /// per build but whose name does not.
    /// </para>
    /// </summary>
    public static Dictionary<string, uint> ResolveStaticSymbols(string romFsPath, IEnumerable<string> names)
    {
        var found = new Dictionary<string, uint>(StringComparer.Ordinal);
        string crs = Path.Combine(romFsPath, "static.crs");
        if (!File.Exists(crs)) return found;

        byte[] b = File.ReadAllBytes(crs);
        if (b.Length < 0x140 || Encoding.ASCII.GetString(b, 0x80, 4) != "CRO0") return found;

        uint U(int o) => BitConverter.ToUInt32(b, o);
        string Str(uint off)
        {
            if (off == 0 || off >= b.Length) return "";
            int e = (int)off; while (e < b.Length && b[e] != 0) e++;
            return Encoding.ASCII.GetString(b, (int)off, e - (int)off);
        }

        uint segTab = U(0xC8), exTab = U(0xD0), exNum = U(0xD4);
        var want = new HashSet<string>(names, StringComparer.Ordinal);
        for (int i = 0; i < exNum; i++)
        {
            string name = Str(U((int)exTab + i * 8));
            if (!want.Contains(name)) continue;
            uint tag = U((int)exTab + i * 8 + 4);
            uint seg = tag & 0xF, off = tag >> 4;
            found[name] = U((int)segTab + (int)seg * 12) + off;
        }
        return found;
    }
}
