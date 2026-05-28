using pk3DS.Core;
using pk3DS.Core.CTR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.Modding;

namespace pk3DS.WinForms;

public partial class MartEditor7UU : Form
{
    private string CROPath { get { return Path.Combine(Main.RomFSPath, "Shop.cro"); } }
    private byte[] data;
    private byte[] len_Items;
    private byte[] len_BPItem;

    private int ofs_Counts = -1;
    private int ofs_CountsBP = -1;
    private int ofs_Items = -1;
    private int ofs_BP = -1;
    private int ofs_Tutors = -1;
    private int ptr_Items = -1;
    private int ptr_BP = -1;
    private int ptr_Counts = -1;
    private int cachedCodeOfs = -1;

    private string path_Counts => Path.Combine(Main.RomFSPath, "mart_counts_ofs.txt");
    private string path_Items => Path.Combine(Main.RomFSPath, "mart_items_ofs.txt");
    private string path_BP => Path.Combine(Main.RomFSPath, "mart_bp_ofs.txt");
    private string path_Tutors => Path.Combine(Main.RomFSPath, "mart_tutor_ofs.txt");
    private string codeOfsFile { get { return Path.Combine(Main.ExeFSPath, "mart_code_offset.txt"); } }
    
    // Mega Safe Zone: Large unused padding in Segment 2 (Data)
    private const int SafeZoneBase = 0x5800; 
    private const int SafeZoneSize = 2048;

    public MartEditor7UU()
    {
        if (!File.Exists(CROPath))
        {
            WinFormsUtil.Error("CRO does not exist! Closing.", CROPath);
            Close();
        }
        InitializeComponent();

        data = File.ReadAllBytes(CROPath);
        LoadOffsets();

        // Safety: Ensure counts are loaded AFTER we are sure about ofs_Counts
        if (ofs_Counts > 0 && ofs_CountsBP > 0)
        {
            len_BPItem = data.Skip(ofs_CountsBP).Take(10).ToArray();
            len_Items = data.Skip(ofs_Counts).Take(28).ToArray();
        }
        else
        {
            len_BPItem = new byte[10];
            len_Items = new byte[28];
        }

        itemlist[0] = "";
        SetupDGV();
        CB_Location.Items.AddRange(locations);
        CB_LocationBPItem.Items.AddRange(locationsBP);
        CB_Location.SelectedIndex =
            CB_LocationBPItem.SelectedIndex = 0;

        B_Add.Enabled = B_Del.Enabled = B_AddBP.Enabled = B_DelBP.Enabled = true;
    }

    private void LoadOffsets()
    {
        // Try RPT first (Full Compatibility Mode)
        int rpt_tutorSizes = ResearchEngine.GetRelocationPatchTarget(data, 0x03C);
        int rpt_bpSizes = ResearchEngine.GetRelocationPatchTarget(data, 0x030);
        int rpt_pokeSizes = ResearchEngine.GetRelocationPatchTarget(data, 0x024);

        if (rpt_pokeSizes != -1 && rpt_bpSizes != -1)
        {
            ofs_CountsBP = rpt_bpSizes;
            ofs_Counts = rpt_pokeSizes;
            ofs_Tutors = rpt_tutorSizes;
            Text = "Mart Editor (RPT Mode)";

            len_BPItem = data.Skip(ofs_CountsBP).Take(10).ToArray();
            len_Items = data.Skip(ofs_Counts).Take(28).ToArray();
        }
        else
        {
            if (File.Exists(path_Counts)) int.TryParse(File.ReadAllText(path_Counts), out ofs_Counts);
            if (File.Exists(path_Items)) int.TryParse(File.ReadAllText(path_Items), out ofs_Items);
            if (File.Exists(path_BP)) int.TryParse(File.ReadAllText(path_BP), out ofs_BP);
            if (File.Exists(path_Tutors)) int.TryParse(File.ReadAllText(path_Tutors), out ofs_Tutors);

            bool scanNeeded = ofs_Counts <= 0 || ofs_Items <= 0 || ofs_BP <= 0 || ofs_Tutors <= 0;
            if (scanNeeded) ScanForSignatures();
            Text = "Mart Editor (Legacy Mode)";
        }
    }

    private void ScanForSignatures()
    {
        // Immutable Tutor counts are the safest anchor: [15, 17, 17, 15]
        byte[] sig_counts = [0x0F, 0x11, 0x11, 0x0F];
        int idx_c = Util.IndexOfBytes(data, sig_counts, 0, data.Length - sig_counts.Length);
        // counts starts at idx_c - 28, bp starts at idx_c - 38
        if (idx_c >= 0)
        {
            ofs_Tutors = idx_c;
            ofs_Counts = idx_c - 28;
            ofs_CountsBP = idx_c - 38;
        }
        else
        {
            ofs_Counts = 0x52D2 - 28;
            ofs_CountsBP = 0x52D2 - 38;
        }
        ofs_Items = 0x50BC;
        ofs_BP = 0x5412;
        ofs_Tutors = 0x54DE;

        SaveOffsets();
    }

    private void SaveOffsets()
    {
        File.WriteAllText(path_Counts, ofs_Counts.ToString());
        File.WriteAllText(path_Items, ofs_Items.ToString());
        File.WriteAllText(path_BP, ofs_BP.ToString());
        File.WriteAllText(path_Tutors, ofs_Tutors.ToString());
    }

    private void UpdateOffsets(int insertionPoint, int change)
    {
        if (ofs_Counts >= insertionPoint) ofs_Counts += change;
        if (ofs_Items >= insertionPoint) ofs_Items += change;
        if (ofs_BP >= insertionPoint) ofs_BP += change;
        if (ofs_Tutors >= insertionPoint) ofs_Tutors += change;

        // Update Pointer Tables in the binary
        SyncPointerTables(insertionPoint, change);

        SaveOffsets();
    }

    private void SyncPointerTables(int insertionPoint, int change)
    {
        if (ptr_Items >= 0)
        {
            for (int i = 0; i < 28; i++) // Mart Item Pointers
            {
                int pOfs = ptr_Items + i * 4;
                if (pOfs + 4 > data.Length) break;
                uint val = BitConverter.ToUInt32(data, pOfs);
                if (val >= (uint)insertionPoint) 
                    Array.Copy(BitConverter.GetBytes(val + (uint)change), 0, data, pOfs, 4);
            }
        }
        if (ptr_BP >= 0)
        {
            for (int i = 0; i < 10; i++) // BP Item Pointers
            {
                int pOfs = ptr_BP + i * 4;
                if (pOfs + 4 > data.Length) break;
                uint val = BitConverter.ToUInt32(data, pOfs);
                if (val >= (uint)insertionPoint) 
                    Array.Copy(BitConverter.GetBytes(val + (uint)change), 0, data, pOfs, 4);
            }
        }
        if (ptr_Counts >= 0)
        {
            for (int i = 0; i < 3; i++) // Pointer to Counts Tables (Mart, BP, Tutor)
            {
                int pOfs = ptr_Counts + i * 4;
                uint val = BitConverter.ToUInt32(data, pOfs);
                if (val >= (uint)insertionPoint) 
                    Array.Copy(BitConverter.GetBytes(val + change), 0, data, pOfs, 4);
            }
        }
    }
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);

    #region Tables
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
        "Route 3 [X Items]",
        "Konikoni City [X Items]",
        "Tapu Village [X Items]",
        "Mount Lanakila [X Items]",
    ];

    private readonly string[] locationsBP = {
        "Battle Royale (Left)",
        "Battle Royale (Middle)",
        "Battle Royale (Right)",
        "Battle Tree (Left)",
        "Battle Tree (Middle)",
        "Battle Tree (Right)",
    };

    #endregion

    private void B_Save_Click(object sender, EventArgs e)
    {
        if (entryItem > -1) SetListItem();
        if (entryBPItem > -1) SetListBPItem();
        CROUtil.UpdateHashes(data);
        File.WriteAllBytes(CROPath, data);
        SyncMartsToCodeBin();
        Close();
    }

    private void B_Cancel_Click(object sender, EventArgs e) => Close();

    private void SetupDGV()
    {
        dgvItem.Items.Clear();
        dgvItem.Items.AddRange(itemlist);
        dgv.DataError += (s, e) => e.ThrowException = false;
        dgvbp.DataError += (s, e) => e.ThrowException = false;
    }

    private int entryItem = -1;
    private int entryBPItem = -1;

    private void ChangeIndexItem(object sender, EventArgs e)
    {
        if (entryItem > -1) SetListItem();
        entryItem = CB_Location.SelectedIndex;
        if (entryItem >= 0) GetListItem();
    }

    private void ChangeIndexBPItem(object sender, EventArgs e)
    {
        if (entryBPItem > -1) SetListBPItem();
        entryBPItem = CB_LocationBPItem.SelectedIndex;
        if (entryBPItem >= 0) GetListBPItem();
    }

    private static readonly uint[] MartPatchAddrs = {
        0x594, 0x5A0, 0x5AC, 0x5B8, 0x5C4, 0x5D0, 0x5DC, 0x5E8, // Trials 0-7 (Indices 0-7)
        0x5F4, // Konikoni Incense (8)
        0x3F0, // Konikoni Herb (9)
        0x42C, // Hau'oli X Items (10)
        0x438, // Route 2 Misc (11)
        0x434, // Heahea TM (12)
        0x450, // Royal Avenue TMs (13)
        0x45C, // Route 8 (14)
        0x474, // Paniola Town (15)
        0x48C, // Malie City [TMs] (16)
        0x4B0, // Mount Hokulani (17)
        0x4BC, // Seafolk Village [TMs] (18)
        0x3FC, // Konikoni City [TMs] (19)
        0x3FC, // Konikoni City [Stones] (20)
        0x408, // Thrifty Megamart, Left (21)
        0x414, // Thrifty Megamart, Middle (22)
        0x420, // Thrifty Megamart, Right (23)
        0x480, // Route 3 [X Items] (24)
        0x468, // Konikoni City [X Items] (25)
        0x498, // Tapu Village [X Items] (26)
        0x4A4, // Mount Lanakila [X Items] (27)
    };

    private static readonly uint[] BPPatchAddrs = {
        0x504, // Royale Left (0)
        0x510, // Royale Middle (1)
        0x51C, // Royale Right (2)
        0x528, // Tree Left (3)
        0x534, // Tree Middle (4)
        0x540, // Tree Right (5)
    };

    private int GetShopOffset(int shopIdx, bool isBP = false)
    {
        if (isBP)
        {
            if (shopIdx >= BPPatchAddrs.Length) return -1;
            int rptOfs = ResearchEngine.GetRelocationPatchTarget(data, BPPatchAddrs[shopIdx]);
            return rptOfs;
        }
        else
        {
            if (shopIdx < MartPatchAddrs.Length)
            {
                int rptOfs = ResearchEngine.GetRelocationPatchTarget(data, MartPatchAddrs[shopIdx]);
                if (rptOfs != -1) return rptOfs;
            }
            // Fallback: Legacy offset calculation
            return ofs_Items + (len_Items.Take(shopIdx).Sum(z => z) * 2);
        }
    }

    private void GetListItem()
    {
        try
        {
            if (entryItem < 0 || entryItem >= len_Items.Length) 
                return;

            dgv.Rows.Clear();
            int count = len_Items[entryItem];
            if (count > 200) count = 200;
            
            if (count > 0)
                dgv.Rows.Add(count);
            int ofs = GetShopOffset(entryItem, false);
            if (ofs == -1) return;

            for (int i = 0; i < count; i++)
            {
                if (ofs + (2 * i) + 1 >= data.Length) break;
                ushort itemID = BitConverter.ToUInt16(data, ofs + (2 * i));
                
                dgv.Rows[i].Cells[0].Value = i.ToString();
                string val = (itemID < itemlist.Length) ? itemlist[itemID] : $"(Unknown: {itemID})";

                var cell = (DataGridViewComboBoxCell)dgv.Rows[i].Cells[1];
                if (!cell.Items.Contains(val)) cell.Items.Add(val);
                cell.Value = val;
            }
        }
        catch (Exception ex) { WinFormsUtil.Error("Crash in GetListItem", ex.ToString()); }
    }

    private void GetListBPItem()
    {
        try
        {
            if (entryBPItem < 0 || entryBPItem >= len_BPItem.Length) 
                return;

            dgvbp.Rows.Clear();
            int count = len_BPItem[entryBPItem];
            if (count > 100) count = 100;

            if (count > 0)
                dgvbp.Rows.Add(count);
            int ofs = GetShopOffset(entryBPItem, true);
            if (ofs == -1) return;

            dgvItemBP.Items.Clear();
            dgvItemBP.Items.AddRange(itemlist);

            int entrySize = 4;
            for (int i = 0; i < count; i++)
            {
                int m_ofs = ofs + (entrySize * i);
                if (m_ofs + entrySize > data.Length) break;

                ushort itemID = BitConverter.ToUInt16(data, m_ofs);
                ushort price = BitConverter.ToUInt16(data, m_ofs + 2);
                
                dgvbp.Rows[i].Cells[0].Value = i.ToString();
                string val = (itemID < itemlist.Length) ? itemlist[itemID] : $"(Unknown: {itemID})";

                var cell = (DataGridViewComboBoxCell)dgvbp.Rows[i].Cells[1];
                cell.Value = val;
                dgvbp.Rows[i].Cells[2].Value = price.ToString();
            }
        }
        catch (Exception ex) { WinFormsUtil.Error("Crash in GetListBPItem", ex.ToString()); }
    }

    private int FindSafeSpace(int bytes)
    {
        // Simple allocator: find first block of 0xFFFFs (unallocated/padding) in the safe zone
        for (int i = 0; i <= SafeZoneSize - bytes; i += 2)
        {
            bool free = true;
            for (int j = 0; j < bytes; j += 2)
            {
                ushort val = BitConverter.ToUInt16(data, SafeZoneBase + i + j);
                if (val != 0xFFFF && val != 0x0000)
                {
                    free = false;
                    break;
                }
            }
            if (free) return SafeZoneBase + i;
        }
        return -1;
    }

    private void SetListItem()
    {
        int count = dgv.Rows.Count;
        int shopOfs = GetShopOffset(entryItem);
        if (shopOfs <= 0) return;

        if (count > len_Items[entryItem])
        {
            // Expansion into Mega Safe Zone
            int required = (count + 1) * 2; // +1 for terminator
            int freeOfs = FindSafeSpace(required);
            if (freeOfs == -1)
            {
                WinFormsUtil.Alert("No space left in Safe Zone for expansion.");
                return;
            }
            
            // Wipe the rest of the zone with Quit markers on first expansion if needed
            if (BitConverter.ToUInt16(data, SafeZoneBase) == 0)
            {
                for (int i = 0; i < SafeZoneSize; i += 2)
                    BitConverter.GetBytes((ushort)0xFFFF).CopyTo(data, SafeZoneBase + i);
            }

            ResearchEngine.RepointRelocationByOffset(data, MartPatchAddrs[entryItem], (uint)freeOfs);
            shopOfs = freeOfs;
        }

        for (int i = 0; i < count; i++)
        {
            int item = Array.IndexOf(itemlist, dgv.Rows[i].Cells[1].Value);
            int m_ofs = shopOfs + (2 * i);
            BitConverter.GetBytes((ushort)item).CopyTo(data, m_ofs);
        }
        
        data[ofs_Counts + entryItem] = (byte)count;
        len_Items[entryItem] = (byte)count;
    }

    private void SetListBPItem()
    {
        if (entryBPItem < 0 || entryBPItem >= len_BPItem.Length) return;
        int count = dgvbp.Rows.Count;
        int shopOfs = GetShopOffset(entryBPItem, true);
        if (shopOfs <= 0) return;

        int entrySize = 4; // USUM: moves are always 4 bytes (Price + Move)

        if (count > len_BPItem[entryBPItem])
        {
            // Expansion into Mega Safe Zone
            int required = (count + 1) * entrySize;
            int freeOfs = FindSafeSpace(required);
            if (freeOfs == -1)
            {
                WinFormsUtil.Alert("No space left in Safe Zone for expansion.");
                return;
            }

            ResearchEngine.RepointRelocationByOffset(data, BPPatchAddrs[entryBPItem], (uint)freeOfs);
            shopOfs = freeOfs;
        }

        for (int i = 0; i < count; i++)
        {
            int item = Array.IndexOf(itemlist, dgvbp.Rows[i].Cells[1].Value);
            uint price = 4; uint.TryParse(dgvbp.Rows[i].Cells[2].Value.ToString(), out price);

            int m_ofs = shopOfs + (entrySize * i);
            BitConverter.GetBytes((ushort)item).CopyTo(data, m_ofs);
            BitConverter.GetBytes((ushort)price).CopyTo(data, m_ofs + 2);
        }
        
        data[ofs_CountsBP + entryBPItem] = (byte)count;
        len_BPItem[entryBPItem] = (byte)count;
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        switch (tabControl1.SelectedIndex)
        {
            case 0:
                RandomizeItems();
                break;
            case 1:
                RandomizeBPItems();
                break;
        }
    }

    private void RandomizeItems()
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Randomize mart inventories?"))
            return;

        int[] validItems = Randomizer.GetRandomItemList();

        int ctr = 0;
        Util.Shuffle(validItems);

        bool specialOnly = DialogResult.Yes == WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize only special marts?", "Will leave regular necessities intact.");
        int start = specialOnly ? 8 : 0;
        for (int i = start; i < CB_Location.Items.Count; i++)
        {
            CB_Location.SelectedIndex = i;
            for (int r = 0; r < dgv.Rows.Count; r++)
            {
                int currentItem = Array.IndexOf(itemlist, dgv.Rows[r].Cells[1].Value);
                if (CHK_XItems.Checked && XItems.Contains(currentItem))
                    continue;
                if (BannedItems.Contains(currentItem))
                    continue;
                dgv.Rows[r].Cells[1].Value = itemlist[validItems[ctr++]];
                if (ctr <= validItems.Length) continue;
                Util.Shuffle(validItems); ctr = 0;
            }
        }
        WinFormsUtil.Alert("Randomized!");
    }

    private void RandomizeBPItems()
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Randomize BP inventories?"))
            return;

        int[] validItems = Randomizer.GetRandomItemList();

        int ctr = 0;
        Util.Shuffle(validItems);

        for (int i = 0; i < CB_LocationBPItem.Items.Count; i++)
        {
            CB_LocationBPItem.SelectedIndex = i;
            for (int r = 0; r < dgvbp.Rows.Count; r++)
            {
                dgvbp.Rows[r].Cells[1].Value = itemlist[validItems[ctr++]];
                if (ctr <= validItems.Length) continue;
                Util.Shuffle(validItems); ctr = 0;
            }
        }
        WinFormsUtil.Alert("Randomized!");
    }

    internal static readonly HashSet<int> XItems = [055, 056, 057, 058, 059, 060, 061, 062];

    /// <summary>
    /// Just TMs & HMs; don't want these to be changed; if changed, they are not available elsewhere ingame.
    /// </summary>
    internal static readonly HashSet<int> BannedItems =
    [
        328, 329, 330, 331, 332, 333, 334, 335, 336, 337, 338, 339, 340, 341, 342, 343, 344, 345, 346, 347, 348,
        349, 350, 351, 352, 353, 354, 355, 356, 357, 358, 359, 360, 361, 362, 363, 364, 365, 366, 367, 368, 369,
        370, 371, 372, 373, 374, 375, 376, 377, 378, 379, 380, 381, 382, 383, 384, 385, 386, 387, 388, 389, 390,
        391, 392, 393, 394, 395, 396, 397, 398, 399, 400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411,
        412, 413, 414, 415, 416, 417, 418, 419, 420, 421, 422, 423, 424, 425, 426, 427, 618, 619, 620, 690, 691,
        692, 693, 694, 701, 737,
    ];

    private void B_Add_Click(object sender, EventArgs e)
    {
        bool isBP = tabControl1.SelectedIndex == 1;
        if (isBP)
        {
            dgvbp.Rows.Add(1);
            int idx = dgvbp.Rows.Count - 1;
            dgvbp.Rows[idx].Cells[0].Value = idx.ToString();
            dgvbp.Rows[idx].Cells[1].Value = itemlist[1];
            dgvbp.Rows[idx].Cells[2].Value = "1";
        }
        else
        {
            dgv.Rows.Add(1);
            int idx = dgv.Rows.Count - 1;
            dgv.Rows[idx].Cells[0].Value = idx.ToString();
            dgv.Rows[idx].Cells[1].Value = itemlist[1];
        }
    }

    private void B_Del_Click(object sender, EventArgs e)
    {
        bool isBP = tabControl1.SelectedIndex == 1;
        if (isBP && dgvbp.Rows.Count > 0) dgvbp.Rows.RemoveAt(dgvbp.Rows.Count - 1);
        else if (!isBP && dgv.Rows.Count > 0) dgv.Rows.RemoveAt(dgv.Rows.Count - 1);
    }

    private void SyncMartsToCodeBin()
    {
        string binName = File.Exists(Path.Combine(Main.ExeFSPath, ".code.bin")) ? ".code.bin" : "code.bin";
        string codePath = Path.Combine(Main.ExeFSPath, binName);
        if (!File.Exists(codePath)) return;

        byte[] codeBin = File.ReadAllBytes(codePath);
        
        // 1. Sync Standard Mart Counts
        if (File.Exists(codeOfsFile))
            int.TryParse(File.ReadAllText(codeOfsFile), out cachedCodeOfs);

        if (cachedCodeOfs <= 0)
        {
            // Verified USUM Trial Mart Counts: 9, 12, 14, 16, 18, 20, 22, 24
            byte[] pat = [0x09, 0x00, 0x0C, 0x00, 0x0E, 0x00, 0x10, 0x00, 0x12, 0x00, 0x14, 0x00, 0x16, 0x00, 0x18, 0x00];
            int idx = Util.IndexOfBytes(codeBin, pat, 0x100000, 0);
            if (idx >= 0)
            {
                cachedCodeOfs = idx;
                File.WriteAllText(codeOfsFile, cachedCodeOfs.ToString());
            }
        }

        if (cachedCodeOfs > 0)
        {
            for (int i = 0; i < len_Items.Length; i++)
            {
                int dest = cachedCodeOfs + i * 2;
                if (dest + 1 < codeBin.Length)
                {
                    codeBin[dest] = len_Items[i];
                    codeBin[dest + 1] = 0;
                }
            }
        }

        // 2. Sync BP Shop Counts
        byte[] patBP = [0x08, 0x00, 0x07, 0x00, 0x12, 0x00, 0x0C, 0x00, 0x15, 0x00, 0x10, 0x00];
        int bpOfs = Util.IndexOfBytes(codeBin, patBP, 0x100000, 0);
        if (bpOfs >= 0)
        {
            // The code.bin table usually has 7 entries for BP shops (Royale x3, Tree x3, Beach)
            for (int i = 0; i < 7; i++)
            {
                int dest = bpOfs + i * 2;
                if (dest + 1 < codeBin.Length && i < len_BPItem.Length)
                {
                    codeBin[dest] = len_BPItem[i];
                    codeBin[dest + 1] = 0;
                }
            }
        }

        File.WriteAllBytes(codePath, codeBin);
    }

private void SyncExecutablePointers(byte[] codeBin)
    {
        // Define the mapping of Vanilla -> Current offsets
        // Vanilla USUM: Items=0x50BC, Counts=0x52D2, BP=0x5412, Tutor=0x54DE
        Dictionary<uint, uint> map = new Dictionary<uint, uint>();
        
        // Only repoint if the table has actually been moved
        if (ofs_Items > 0 && ofs_Items != 0x50BC) map.Add(0x50BC, (uint)ofs_Items);
        if (ofs_Counts > 0 && ofs_Counts != 0x52D2) map.Add(0x52D2, (uint)ofs_Counts);
        if (ofs_BP > 0 && ofs_BP != 0x5412) map.Add(0x5412, (uint)ofs_BP);
        if (ofs_Tutors > 0 && ofs_Tutors != 0x54DE) map.Add(0x54DE, (uint)ofs_Tutors);

        if (map.Count == 0) return;

        int repointCount = 0;

        for (int i = 0; i < codeBin.Length - 4; i += 4)
        {
            uint val = BitConverter.ToUInt32(codeBin, i);
            if (map.ContainsKey(val))
            {
                // Strict Verification: Ensure this is a real pointer referenced by an LDR instruction
                if (VerifyLiteralPoolReference(codeBin, i))
                {
                    Array.Copy(BitConverter.GetBytes(map[val]), 0, codeBin, i, 4);
                    repointCount++;
                }
            }
        }
        
        if (repointCount > 0)
            Console.WriteLine($"Successfully repointed {repointCount} table references safely.");
    }

    private bool VerifyLiteralPoolReference(byte[] codeBin, int poolOffset)
    {
        // Search up to 1024 bytes backwards for the instruction that calls this literal pool address
        int searchStart = Math.Max(0, poolOffset - 1024);
        
        for (int i = poolOffset - 4; i >= searchStart; i -= 4)
        {
            uint instr = BitConverter.ToUInt32(codeBin, i);
            
            // ARM Heuristic: LDR Rd, [PC, #imm12] (Positive Offset) -> E59Fxxxx
            if ((instr & 0x0FFF0000) == 0x059F0000) 
            {
                uint imm = instr & 0xFFF;
                uint pc = (uint)i + 8; // In ARM32, PC is always ahead by 8 bytes
                if (pc + imm == (uint)poolOffset) return true;
            }
            // ARM Heuristic: LDR Rd, [PC, #-imm12] (Negative Offset) -> E51Fxxxx
            else if ((instr & 0x0FFF0000) == 0x051F0000)
            {
                uint imm = instr & 0xFFF;
                uint pc = (uint)i + 8;
                if (pc - imm == (uint)poolOffset) return true;
            }
        }
        
        // If no LDR instruction directly mathematically references this offset, it's a coincidence. Ignore it.
        return false;
    }
}