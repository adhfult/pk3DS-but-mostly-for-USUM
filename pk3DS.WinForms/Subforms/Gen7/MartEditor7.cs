using pk3DS.Core;
using pk3DS.Core.CTR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class MartEditor7 : Form
{
    private readonly string CROPath = Path.Combine(Main.RomFSPath, "Shop.cro");
    private byte[] data;
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);

    internal static readonly List<int> XItems = new List<int> { 0x37, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x163 };
    internal static readonly List<int> BannedItems = new List<int> { 0x1B, 0x4B, 0x4C, 0x4D, 0x12, 0x121, 0x122, 0x123, 0x124 };

    private readonly byte[] Signature = 
    [
        0x2D, 0x00, 0x00, 0x00, 0x3B, 0x00, 0x00, 0x00, 0x2F, 0x00, 0x00, 0x00, 0x3D, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00,
        0x10, 0x00, 0x00, 0x00, 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00,
    ];

    private readonly byte[] BPSignature = 
    [
        0x09, 0x0B, 0x0D, 0x0F, 0x11, 0x13, 0x14, 0x15, 0x09, 0x04, 0x08, 0x0C, 0x05, 0x04, 0x0B, 0x03,
        0x0A, 0x06, 0x0A, 0x06, 0x04, 0x05, 0x07, 0x01,
    ];

    private int[] entries = [9, 11, 13, 15, 17, 19, 20, 21, 9, 4, 8, 12, 5, 4, 11, 3, 10, 6, 10, 6, 4, 5, 7, 1];
    private int[] entriesBP = [8, 7, 18, 12, 21, 16];

    private readonly string[] locations =
    [
        "No Trials", "1 Trial", "2 Trials", "3 Trials", "4 Trials", "5 Trials", "6 Trials", "7 Trials",
        "Konikoni City [Incenses]",
        "Konikoni City [Herbs]",
        "Hau'oli City [X Items]",
        "Route 2 [Misc]",
        "Heahea City [TMs]",
        "Royal Avenue [TMs]",
        "Route 8 [Misc]",
        "Paniola Town [Poké Balls]",
        "Malie City [TMs]",
        "Mount Hokulani [Vitamins]",
        "Seafolk Village [TMs]",
        "Konikoni City [TMs]",
        "Konikoni City [Stones]",
        "Thrifty Megamart, Left [Poké Balls]",
        "Thrifty Megamart, Middle [Misc]",
        "Thrifty Megamart, Right [Strange Souvenir]",
    ];

    private readonly string[] locationsBP =
    [
        "Battle Royal Dome [Medicine]",
        "Battle Royal Dome [EV Training]",
        "Battle Royal Dome [Held Items]",
        "Battle Tree [Trade Evolution Items]",
        "Battle Tree [Held Items]",
        "Battle Tree [Mega Stones]",
    ];

    public MartEditor7()
    {
        if (!File.Exists(CROPath))
        {
            WinFormsUtil.Error("CRO does not exist! Closing.", CROPath);
            Close();
            return;
        }
        InitializeComponent();

        data = File.ReadAllBytes(CROPath);
        itemlist[0] = "";
        SetupDGV();
        ReadEntriesFromCodeBin();
        CB_Location.Items.AddRange(locations);
        CB_LocationBP.Items.AddRange(locationsBP);
        CB_Location.SelectedIndex = 0;
        CB_LocationBP.SelectedIndex = 0;
    }

    private void ReadEntriesFromCodeBin()
    {
        int listStart = offset;
        if (listStart > Signature.Length)
        {
            var derived = pk3DS.Core.Randomizers.MartRandomizer.ResolveEntryCounts(
                data, listStart, Math.Min(listStart + (entries.Sum() * 2) + 8, data.Length), CROPath);
            for (int i = 0; i < entries.Length && i < derived.Length; i++)
                entries[i] = derived[i];
        }

        var savedBP = pk3DS.Core.Randomizers.MartRandomizer.LoadCounts(CROPath);
        if (savedBP is { } sc)
            for (int i = 0; i < entriesBP.Length && i < sc.BP.Length; i++)
                entriesBP[i] = sc.BP[i];

        // The code.bin table below does not exist in any USUM build tested, so this normally does
        // nothing; it is kept for ROMs that do carry one.
        if (string.IsNullOrWhiteSpace(Main.ExeFSPath)) return;
        string binName = File.Exists(Path.Combine(Main.ExeFSPath, ".code.bin")) ? ".code.bin" : "code.bin";
        string fullCodePath = Path.Combine(Main.ExeFSPath, binName);
        if (!File.Exists(fullCodePath)) return;

        byte[] codeBin = File.ReadAllBytes(fullCodePath);

        byte[] pat = { 9, 0, 11, 0, 13, 0, 15, 0, 17, 0, 19, 0, 20, 0, 21, 0, 9, 0, 4, 0, 8, 0 };
        int martOfs = Util.IndexOfBytes(codeBin, pat, 0x100000, 0);
        if (martOfs > 0)
        {
            for (int i = 0; i < entries.Length; i++)
                entries[i] = codeBin[martOfs + i * 2];
        }

        byte[] patBP = { 8, 0, 7, 0, 18, 0, 12, 0, 21, 0, 16, 0 };
        int bpOfs = Util.IndexOfBytes(codeBin, patBP, 0x100000, 0);
        if (bpOfs > 0)
        {
            for (int i = 0; i < entriesBP.Length; i++)
                entriesBP[i] = codeBin[bpOfs + i * 2];
        }
    }

    private int GetRodataOffset()
    {
        uint segmentTableOffset = BitConverter.ToUInt32(data, 0xC8);
        return (int)BitConverter.ToUInt32(data, (int)segmentTableOffset + 0x0C); 
    }

    private int offset => Util.IndexOfBytes(data, Signature, GetRodataOffset(), 0) + Signature.Length;
    private int offsetBP => Util.IndexOfBytes(data, BPSignature, GetRodataOffset(), 0) + BPSignature.Length;

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (entry > -1) SetList();
        if (entryBP > -1) SetListBP();
        CROUtil.UpdateHashes(data);
        File.WriteAllBytes(CROPath, data);
        SyncMartsToCodeBin();
        Close();
    }

    private void SyncMartsToCodeBin()
    {
        string binName = File.Exists(Path.Combine(Main.ExeFSPath, ".code.bin")) ? ".code.bin" : "code.bin";
        string fullCodePath = Path.Combine(Main.ExeFSPath, binName);
        if (!File.Exists(fullCodePath)) return;

        byte[] codeBin = File.ReadAllBytes(fullCodePath);
        
        // Dynamic search for Mart Limit table in code.bin
        byte[] pat = { 9, 0, 11, 0, 13, 0, 15, 0, 17, 0, 19, 0, 20, 0, 21, 0, 9, 0, 4, 0, 8, 0 };
        int martOfs = Util.IndexOfBytes(codeBin, pat, 0x100000, 0);
        if (martOfs > 0)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                codeBin[martOfs + i * 2] = (byte)entries[i];
            }
        }

        byte[] patBP = { 8, 0, 7, 0, 18, 0, 12, 0, 21, 0, 16, 0 };
        int bpOfs = Util.IndexOfBytes(codeBin, patBP, 0x100000, 0);
        if (bpOfs > 0)
        {
            for (int i = 0; i < entriesBP.Length; i++)
            {
                codeBin[bpOfs + i * 2] = (byte)entriesBP[i];
            }
        }

        File.WriteAllBytes(fullCodePath, codeBin);
    }

    private void B_Cancel_Click(object sender, EventArgs e) => Close();

    private void SetupDGV()
    {
        dgvItem.Items.AddRange(itemlist);
        dgvItemBP.Items.AddRange(itemlist);
    }

    private int entry = -1;
    private int entryBP = -1;

    private void ChangeIndex(object sender, EventArgs e)
    {
        if (entry > -1) SetList();
        entry = CB_Location.SelectedIndex;
        GetList();
    }

    private void GetList()
    {
        dgv.Rows.Clear();
        int count = entries[entry];
        dgv.Rows.Add(count);
        int currentOfs = offset;
        for (int i = 0; i < entry; i++) currentOfs += 2 * entries[i];
        for (int i = 0; i < count; i++)
        {
            dgv.Rows[i].Cells[0].Value = i.ToString();
            dgv.Rows[i].Cells[1].Value = itemlist[BitConverter.ToUInt16(data, currentOfs + (2 * i))];
        }
        UpdateItemSprite();
    }

    private void UpdateItemSprite()
    {
        if (dgv.CurrentRow == null) return;
        string itemName = dgv.CurrentRow.Cells[1].Value?.ToString() ?? "";
        int itemId = Array.IndexOf(itemlist, itemName);
        if (itemId > 0)
        {
            // Update PB_Item if it was added to the designer, or provide logging/visual hint
        }
    }

    private void SetList()
    {
        int currentOfs = offset;
        for (int i = 0; i < entry; i++) currentOfs += 2 * entries[i];
        int count = dgv.Rows.Count;
        for (int i = 0; i < count; i++)
        {
            int idx = Array.IndexOf(itemlist, dgv.Rows[i].Cells[1].Value);
            Array.Copy(BitConverter.GetBytes((ushort)idx), 0, data, currentOfs + (2 * i), 2);
        }
    }

    private void B_Add_Click(object sender, EventArgs e)
    {
        if (entry < 0) return;
        if (entry > -1) SetList();
        int currentOfs = offset;
        for (int i = 0; i < entry; i++) currentOfs += 2 * entries[i];
        int insertionPoint = currentOfs + (entries[entry] * 2);

        data = CROUtil.ExpandSegment(data, 'r', 2, insertionPoint, 0x01);
        entries[entry]++;
        GetList();
    }

    private void B_Del_Click(object sender, EventArgs e)
    {
        if (dgv.CurrentRow == null) return;
        int rowIdx = dgv.CurrentRow.Index;
        if (entry > -1) SetList();
        int currentOfs = offset;
        for (int i = 0; i < entry; i++) currentOfs += 2 * entries[i];
        int deletionPoint = currentOfs + (rowIdx * 2);

        data = CROUtil.ExpandSegment(data, 'r', -2, deletionPoint);
        entries[entry]--;
        GetList();
    }

    private void ChangeIndexBP(object sender, EventArgs e)
    {
        if (entryBP > -1) SetListBP();
        entryBP = CB_LocationBP.SelectedIndex;
        GetListBP();
    }

    private void GetListBP()
    {
        dgvbp.Rows.Clear();
        int count = entriesBP[entryBP];
        dgvbp.Rows.Add(count);
        int currentOfs = offsetBP;
        for (int i = 0; i < entryBP; i++) currentOfs += 4 * entriesBP[i];
        for (int i = 0; i < count; i++)
        {
            dgvbp.Rows[i].Cells[0].Value = i.ToString();
            dgvbp.Rows[i].Cells[1].Value = itemlist[BitConverter.ToUInt16(data, currentOfs + (4 * i))];
            dgvbp.Rows[i].Cells[2].Value = BitConverter.ToUInt16(data, currentOfs + (4 * i) + 2).ToString();
        }
    }

    private void SetListBP()
    {
        int currentOfs = offsetBP;
        for (int i = 0; i < entryBP; i++) currentOfs += 4 * entriesBP[i];
        int count = dgvbp.Rows.Count;
        for (int i = 0; i < count; i++)
        {
            int item = Array.IndexOf(itemlist, dgvbp.Rows[i].Cells[1].Value);
            Array.Copy(BitConverter.GetBytes((ushort)item), 0, data, currentOfs + (4 * i), 2);
            string p = dgvbp.Rows[i].Cells[2].Value?.ToString() ?? "0";
            if (int.TryParse(p, out var price))
                Array.Copy(BitConverter.GetBytes((ushort)price), 0, data, currentOfs + (4 * i) + 2, 2);
        }
    }

    private void B_AddBP_Click(object sender, EventArgs e)
    {
        if (entryBP < 0) return;
        if (entryBP > -1) SetListBP();
        int currentOfs = offsetBP;
        for (int i = 0; i < entryBP; i++) currentOfs += 4 * entriesBP[i];
        int insertionPoint = currentOfs + (entriesBP[entryBP] * 4);

        data = CROUtil.ExpandSegment(data, 'r', 4, insertionPoint, 0x00);
        entriesBP[entryBP]++;
        GetListBP();
    }

    private void B_DelBP_Click(object sender, EventArgs e)
    {
        if (dgvbp.CurrentRow == null) return;
        int rowIdx = dgvbp.CurrentRow.Index;
        if (entryBP > -1) SetListBP();
        int currentOfs = offsetBP;
        for (int i = 0; i < entryBP; i++) currentOfs += 4 * entriesBP[i];
        int deletionPoint = currentOfs + (rowIdx * 4);

        data = CROUtil.ExpandSegment(data, 'r', -4, deletionPoint);
        entriesBP[entryBP]--;
        GetListBP();
    }

    private void B_ExportTxt_Click(object sender, EventArgs e)
    {
        if (entry > -1) SetList();
        if (entryBP > -1) SetListBP();

        var sfd = new SaveFileDialog { FileName = "Marts.txt", Filter = "Text File|*.txt" };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        var lines = new List<string>();
        for (int loc = 0; loc < locations.Length; loc++)
        {
            lines.Add($"=== {locations[loc]} ===");
            int ofs = offset;
            for (int j = 0; j < loc; j++) ofs += 2 * entries[j];
            for (int i = 0; i < entries[loc]; i++)
                lines.Add($"{i}: {itemlist[BitConverter.ToUInt16(data, ofs + (2 * i))]}");
            lines.Add("");
        }
        for (int loc = 0; loc < locationsBP.Length; loc++)
        {
            lines.Add($"=== BP: {locationsBP[loc]} ===");
            int ofs = offsetBP;
            for (int j = 0; j < loc; j++) ofs += 4 * entriesBP[j];
            for (int i = 0; i < entriesBP[loc]; i++)
            {
                int itemId = BitConverter.ToUInt16(data, ofs + (4 * i));
                int price = BitConverter.ToUInt16(data, ofs + (4 * i) + 2);
                lines.Add($"{i}: {itemlist[itemId]} | {price}");
            }
            lines.Add("");
        }
        File.WriteAllLines(sfd.FileName, lines);
        WinFormsUtil.Alert("Mart data exported!");
        }

        private void B_Randomize_Click(object sender, EventArgs e) => RunCategoryRandomize();

        private void B_RandomizeBP_Click(object sender, EventArgs e) => RunCategoryRandomize();

        private void RunCategoryRandomize()
        {
            // Persist any pending manual edits first — ExecuteCompetitive re-reads Shop.cro fresh
            // from disk, so in-memory-only changes here would otherwise be silently discarded.
            if (entry > -1) SetList();
            if (entryBP > -1) SetListBP();
            CROUtil.UpdateHashes(data);
            File.WriteAllBytes(CROPath, data);

            var martRand = new pk3DS.Core.Randomizers.MartRandomizer(Main.RomFSPath, 2, banBadItems: true, randomizeAllShops: true);
            var (newRegular, newBP) = martRand.ExecuteCompetitive(Main.Config.Info.MaxItemID, Main.Config);

            if (newRegular != null) entries = newRegular;
            if (newBP != null) entriesBP = newBP;

            data = File.ReadAllBytes(CROPath);
            entry = -1;
            entryBP = -1;
            if (CB_Location.SelectedIndex >= 0) ChangeIndex(this, EventArgs.Empty);
            if (CB_LocationBP.SelectedIndex >= 0) ChangeIndexBP(this, EventArgs.Empty);

            WinFormsUtil.Alert("Marts randomized — locations were also expanded to fit their full category lists.");
        }

        /// <summary>
        /// Reads back the format <see cref="B_ExportTxt_Click"/> writes.
        /// <para>
        /// Sections are "=== Location ===" for a normal mart and "=== BP: Location ===" for a BP
        /// shop; each following line is "index: Item Name", with " | price" on the BP side. Slots
        /// are matched by their index within the section, so a file may fill in only some of them.
        /// </para>
        /// </summary>
        private void B_ImportTxt_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { FileName = "Marts.txt", Filter = "Text File|*.txt" };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            int applied = 0;
            var problems = new List<string>();
            int loc = -1;
            bool isBP = false;

            foreach (string raw in File.ReadAllLines(ofd.FileName))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("===") && line.EndsWith("==="))
                {
                    string title = line.Trim('=', ' ');
                    isBP = title.StartsWith("BP:", StringComparison.OrdinalIgnoreCase);
                    if (isBP) title = title[3..].Trim();

                    var table = isBP ? locationsBP : locations;
                    loc = Array.FindIndex(table, s => string.Equals(s, title, StringComparison.OrdinalIgnoreCase));
                    if (loc < 0) problems.Add($"unknown shop \"{title}\"");
                    continue;
                }

                if (loc < 0) continue;

                int colon = line.IndexOf(':');
                if (colon <= 0 || !int.TryParse(line[..colon].Trim(), out int slot)) continue;

                string body = line[(colon + 1)..].Trim();
                int price = -1;
                int bar = body.LastIndexOf('|');
                if (bar >= 0)
                {
                    int.TryParse(body[(bar + 1)..].Trim(), out price);
                    body = body[..bar].Trim();
                }

                int itemId = Array.FindIndex(itemlist, s => string.Equals(s, body, StringComparison.OrdinalIgnoreCase));
                if (itemId < 0) { problems.Add($"unknown item \"{body}\""); continue; }

                if (isBP)
                {
                    if (slot < 0 || slot >= entriesBP[loc]) { problems.Add($"BP slot {slot} out of range"); continue; }
                    int ofs = offsetBP;
                    for (int j = 0; j < loc; j++) ofs += 4 * entriesBP[j];
                    BitConverter.GetBytes((ushort)itemId).CopyTo(data, ofs + (4 * slot));
                    if (price >= 0)
                        BitConverter.GetBytes((ushort)price).CopyTo(data, ofs + (4 * slot) + 2);
                }
                else
                {
                    if (slot < 0 || slot >= entries[loc]) { problems.Add($"slot {slot} out of range"); continue; }
                    int ofs = offset;
                    for (int j = 0; j < loc; j++) ofs += 2 * entries[j];
                    BitConverter.GetBytes((ushort)itemId).CopyTo(data, ofs + (2 * slot));
                }
                applied++;
            }

            // Re-read whichever list is on screen so the change is visible immediately.
            if (entry > -1) GetList();
            if (entryBP > -1) GetListBP();

            string detail = problems.Count == 0
                ? "Every line was matched."
                : $"Skipped {problems.Count}: " + string.Join("; ", problems.Take(10))
                  + (problems.Count > 10 ? "; ..." : "");
            WinFormsUtil.Alert($"Imported {applied} mart slot(s).", detail);
        }
    }
