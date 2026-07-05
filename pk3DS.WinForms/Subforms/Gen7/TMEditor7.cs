using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using pk3DS.Core;
using pk3DS.Core.Modding;
using pk3DS.Core.CTR;

namespace pk3DS.WinForms;

public partial class TMEditor7 : Form
{
    private bool isSplitTablePatchApplied = false;
    public TMEditor7()
    {
        InitializeComponent();
        if (Main.ExeFSPath == null) { WinFormsUtil.Alert("No exeFS code to load."); Close(); }
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code")) { WinFormsUtil.Alert("No .code.bin detected."); Close(); }
        data = File.ReadAllBytes(files[0]);
        if (data.Length % 0x200 != 0) { WinFormsUtil.Alert(".code.bin not decompressed. Aborting."); Close(); }

        // Universal TM Table Detection
        // TM01: Work Up (526), TM02: Dragon Claw (337), TM03: Psyshock (473)
        // Little-endian ushorts: [0x0E, 0x02, 0x51, 0x01, 0xD9, 0x01]
        byte[] tmSig = [0x0E, 0x02, 0x51, 0x01, 0xD9, 0x01];
        int foundOfs = Util.IndexOfBytes(data, tmSig, 0x100000, 0);
        if (foundOfs >= 0)
        {
            offset = foundOfs;
        }
        else
        {
            // Fallback to standard signature search
            offset = Util.IndexOfBytes(data, Signature, 0x400000, 0) + Signature.Length;
            if (Main.Config.USUM) offset += 0x22;
        }
        codebin = files[0];
        movelist[0] = "";


        // Auto-detect expansion start ID
        DetectExpansionStartID();
        
        // Auto-detect expansion from binary by scanning for CMP instructions
        if (File.Exists(codebin))
        {
            int detectedCount = DetectTMCount(data);
            if (detectedCount > 0 && detectedCount != (int)NUD_TMCount.Value)
            {
                skipUpdate = true;
                NUD_TMCount.Value = Math.Min(detectedCount, NUD_TMCount.Maximum);
                skipUpdate = false;
            }
        }

        SetupDGV();
        
        // Auto-detect TM count from code.bin once on load
        if (File.Exists(codebin))
        {
            byte[] codeData = File.ReadAllBytes(codebin);
            int detectedCount = DetectTMCount(codeData);
            if (detectedCount > 0)
            {
                skipUpdate = true;
                NUD_TMCount.Value = Math.Min(detectedCount, NUD_TMCount.Maximum);
                skipUpdate = false;
            }
        }
        
        GetList();
        TB_Offset.Text = offset.ToString("X");
        
        if (isSplitTablePatchApplied)
        {
            TB_Offset.Enabled = false;
        }

        // Show TM expansion info when patch is detected
        int tmCount = (int)NUD_TMCount.Value;
        if (tmCount > 100)
        {
            string msg = $"TM/HM Expansion Patch Detected — {tmCount} TMs\n\n"
                + "Slot Configuration:\n"
                + "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
                + "• TM01–TM107: Uses standard/HM slots.\n"
                + "• TM108+: Mapped across unused items; look at their IDs for more information.";
            WinFormsUtil.Alert(msg);
        }
    }

    private int expandedTMStartID = 960;
    private void DetectExpansionStartID()
    {
        expandedTMStartID = 960; // Default
        try {
            if (File.Exists(codebin)) {
                int count = (int)NUD_TMCount.Value;
                ushort[] defaultItems = GetDefaultTMItems();
                ushort[] itemIDs = ResearchEngine.GetTMItemArray(data, count, defaultItems);
                if (itemIDs.Length > 107 && itemIDs[107] > 0)
                {
                    expandedTMStartID = itemIDs[107];
                }
            }
        } catch { }
    }

    private static readonly byte[] Signature = [0x03, 0x40, 0x03, 0x41, 0x03, 0x42, 0x03, 0x43, 0x03]; // tail end of item::ITEM_CheckBeads
    private readonly string codebin;
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private bool skipUpdate = false;
    private int offset = 0x0059795A; // Default
    private readonly byte[] data;
    private int dataoffset;

    private void GetDataOffset()
    {
        dataoffset = offset; // reset
    }

    private int GetTMOffset(int index)
    {
        if (isSplitTablePatchApplied)
        {
            // Table 1 (TM 01-68) is located 0x1FA bytes before Table 2.
            if (index < 68) return offset - 0x1FA + (2 * index);
            // Table 2 (TM 69+) is located at the original offset.
            else return offset + (2 * (index - 68));
        }

        // TM01 to TM100 are always contiguous from the detected base
        if (index < 100) return offset + (2 * index);

        // For expanded TMs (101+), we check if there's a second table (sandbox)
        // or if they are contiguous. Most expansion patches jump at 108.
        if (index >= 107)
        {
             // If a known sandbox offset is provided in the textbox or research, use it.
             // Otherwise, check for the 108+ sandbox (0x4BB794) if it contains a move ID.
             if (offset < 0x100000 && data.Length > 0x4BB794 + 2)
                 return 0x4BB794 + (2 * (index - 107));
        }
        return offset + (2 * index);
    }

    private void SetupDGV()
    {
        dgvTM.Columns.Clear();
        var dgvIndex = new DataGridViewTextBoxColumn();
        {
            dgvIndex.HeaderText = "Index";
            dgvIndex.DisplayIndex = 0;
            dgvIndex.Width = 45;
            dgvIndex.ReadOnly = true;
            dgvIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvIndex.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        var dgvMove = new DataGridViewComboBoxColumn();
        {
            dgvMove.HeaderText = "Move";
            dgvMove.DisplayIndex = 1;
            dgvMove.Items.AddRange(movelist); // add only the Names

            dgvMove.Width = 133;
            dgvMove.FlatStyle = FlatStyle.Flat;
            dgvMove.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        dgvTM.Columns.Add(dgvIndex);
        dgvTM.Columns.Add(dgvMove);
    }

    private List<ushort> tms = [];

    private void GetList()
    {
        dgvTM.Rows.Clear();
        tms = [];

        // Dynamic Repointing: Parse the offset box
        if (!int.TryParse(TB_Offset.Text, System.Globalization.NumberStyles.HexNumber, null, out int currentOffset))
            currentOffset = offset;

        int count = (int)NUD_TMCount.Value;
        if (currentOffset + (count * 2) > data.Length)
        {
             WinFormsUtil.Alert("Offset is out of bounds for the current code.bin.");
             return;
        }

        ushort[] defaultTMs = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            int offset = GetTMOffset(i);
            if (offset + 1 < data.Length)
                defaultTMs[i] = BitConverter.ToUInt16(data, offset);
        }

        ushort[] tmlist = ResearchEngine.GetTMMoveArray(data, count, defaultTMs);
        tms.AddRange(tmlist);

        ushort[] tmlist = [.. tms];
        for (int i = 0; i < tmlist.Length; i++)
        { 
            dgvTM.Rows.Add(); 
            dgvTM.Rows[i].Cells[0].Value = (i + 1).ToString(); 
            
            ushort moveId = tmlist[i];
            if (moveId >= movelist.Length) moveId = 0; 
            
            dgvTM.Rows[i].Cells[1].Value = movelist[moveId]; 
        }
    }

    /// <summary>
    /// Scans code.bin for the CMP instruction that originally checked #100 (0x64) for TM count.
    /// Only searches near the TM table offset to avoid false positives from unrelated CMP instructions.
    /// Decodes ARM rotated immediates to return the actual patched value.
    /// </summary>
    private int DetectTMCount(byte[] codeData)
    {
        // The original instruction is CMP R0, #0x64 (100) => E3 50 00 64
        // After a TM expansion patch, this immediate is changed to the new count.
        // We ONLY search near the TM table region to avoid matching random CMP instructions
        // elsewhere in the binary (which was causing false positive 128-slot detection).
        
        // Search within a reasonable window around the TM table offset
        int searchStart = Math.Max(0, offset - 0x2000);
        int searchEnd = Math.Min(codeData.Length - 4, offset + 0x2000);
        
        // First pass: look for the exact original CMP R0, #100 (unpatched)
        for (int i = searchStart; i < searchEnd; i += 4)
        {
            uint word = BitConverter.ToUInt32(codeData, i);
            if (word == 0xE3500064) // CMP R0, #0x64 (exactly 100, unpatched)
                return 100; // Original game, no expansion
        }
        
        // Second pass: the CMP was patched — find the new value
        int bestCount = 0;
        for (int i = searchStart; i < searchEnd; i += 4)
        {
            uint word = BitConverter.ToUInt32(codeData, i);
            // Match CMP R0, #imm (condition AL, opcode CMP, Rn=R0)
            if ((word & 0xFFF00000) != 0xE3500000) continue;
            
            // Decode ARM rotated immediate
            uint imm8 = word & 0xFF;
            uint rot = (word >> 8) & 0xF;
            uint value = rot == 0 ? imm8 : (imm8 >> (int)(rot * 2)) | (imm8 << (int)(32 - rot * 2));
            
            // Valid expanded TM counts: 101-128
            if (value > 100 && value <= 128 && (int)value > bestCount)
                bestCount = (int)value;
        }
        
        return bestCount > 0 ? bestCount : 100;
    }

    private void SetList()
    {
        tms = [];
        List<ushort> items = [];
        for (int i = 0; i < dgvTM.Rows.Count; i++)
        {
            if (ushort.TryParse(dgvTM.Rows[i].Cells[1].Value?.ToString(), out ushort itemID))
                items.Add(itemID);
            else
                items.Add(0);

            var val = dgvTM.Rows[i].Cells[2].Value;
            if (val == null) tms.Add(0);
            else tms.Add((ushort)Array.IndexOf(movelist, val.ToString()));
        }

        ushort[] tmlist = [.. tms];

        if (!int.TryParse(TB_Offset.Text, System.Globalization.NumberStyles.HexNumber, null, out int currentOffset))
            currentOffset = offset;

        int count = Math.Min(tmlist.Length, (int)NUD_TMCount.Value);
        for (int i = 0; i < count; i++) 
            Array.Copy(BitConverter.GetBytes(tmlist[i]), 0, data, GetTMOffset(i), 2);

        // Update descriptions
        string[] itemNames = Main.Config.GetText(TextName.ItemNames);
        if (maxItemID >= itemNames.Length)
        {
            Array.Resize(ref itemNames, maxItemID + 1);
            for (int i = 0; i < itemNames.Length; i++)
                if (itemNames[i] == null) itemNames[i] = "???";
        }
        for (int i = 0; i < tmlist.Length; i++) { int target = itemlist[i]; if (target > 0 && target < itemNames.Length) { itemNames[target] = $"TM{(i+1):D3}"; } }
        Main.Config.SetText(TextName.ItemNames, itemNames);
        
        string[] itemDescriptions = Main.Config.GetText(TextName.ItemFlavor);
        if (maxItemID >= itemDescriptions.Length)
        {
            Array.Resize(ref itemDescriptions, maxItemID + 1);
            for (int i = 0; i < itemDescriptions.Length; i++)
                if (itemDescriptions[i] == null) itemDescriptions[i] = "???";
        }
        string[] moveDescriptions = Main.Config.GetText(TextName.MoveFlavor);
        
        // TM01-TM92
        for (int i = 0; i < tmlist.Length; i++)
        {
            int targetID = itemlist[i];
            if (targetID > 0 && targetID < itemDescriptions.Length)
                itemDescriptions[targetID] = moveDescriptions[tmlist[i]];
        }

        Main.Config.SetText(TextName.ItemFlavor, itemDescriptions);

        Main.Config.SaveText(TextName.ItemNames);
        Main.Config.SaveText(TextName.ItemFlavor);

        File.WriteAllBytes(codebin, data);
    }


    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetList();
    }

    private void B_RandomTM_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize TMs?", "Move compatibility will be the same as the base TMs.") != DialogResult.Yes) return;

        int[] randomMoves = Enumerable.Range(1, movelist.Length - 1).Select(i => i).ToArray();
        Util.Shuffle(randomMoves);

        int[] banned = [.. Legal.Z_Moves, .. new[] { 165, 464, 621 }];
        int ctr = 0;

        for (int i = 0; i < dgvTM.Rows.Count; i++)
        {
            int val = Array.IndexOf(movelist, dgvTM.Rows[i].Cells[2].Value);
            if (banned.Contains(val)) continue;
            while (banned.Contains(randomMoves[ctr])) ctr++;

            dgvTM.Rows[i].Cells[2].Value = movelist[randomMoves[ctr++]];
        }
        WinFormsUtil.Alert("Randomized!");
    }

    internal static ushort[] GetTMHMList()
    {
        if (Main.ExeFSPath == null) return [];
        string[] files = Directory.GetFiles(Main.ExeFSPath);
        if (!File.Exists(files[0]) || !Path.GetFileNameWithoutExtension(files[0]).Contains("code")) return [];
        
        byte[] data = File.ReadAllBytes(files[0]);
        if (data.Length % 0x200 != 0) return [];

        // Use universal TM table signature detection
        byte[] tmSig = [0x0E, 0x02, 0x51, 0x01, 0xD9, 0x01];
        int dataoffset = Util.IndexOfBytes(data, tmSig, 0x100000, 0);
        if (dataoffset < 0)
        {
            dataoffset = Util.IndexOfBytes(data, Signature, 0x400000, 0) + Signature.Length;
            if (Main.Config.USUM) dataoffset += 0x22;
        }

        // Static callers always get the base 100 TMs — expansion detection
        // requires the instance context (offset) to avoid false positives.
        int count = 100;

        List<ushort> tms = [];
        for (int i = 0; i < count; i++) 
            tms.Add(BitConverter.ToUInt16(data, dataoffset + (2 * i)));
        return [.. tms];
    }

    private void NUD_TMCount_ValueChanged(object sender, EventArgs e)
    {
        if (skipUpdate) return;
        GetList();
    }

    private void B_ExportTxt_Click(object sender, EventArgs e)
    {
        var sfd = new SaveFileDialog { FileName = "TMs.txt", Filter = "Text File|*.txt" };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        var lines = new List<string>();
        for (int i = 0; i < dgvTM.Rows.Count; i++)
        {
            string moveName = dgvTM.Rows[i].Cells[2].Value?.ToString() ?? "";
            lines.Add($"TM{i + 1:00}: {moveName}");
        }
        File.WriteAllLines(sfd.FileName, lines);
        WinFormsUtil.Alert("TM data exported!");
    }

    private void B_ImportTxt_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        string[] lines = File.ReadAllLines(ofd.FileName);
        int updated = 0;
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;

            // Parse lines like "TM01: Work Up" or "TM01: 526"
            int colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;

            string tmPart = line.Substring(0, colonIdx).Trim();
            string movePart = line.Substring(colonIdx + 1).Trim();

            // Extract TM number
            if (!tmPart.StartsWith("TM", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(tmPart.Substring(2), out int tmNum)) continue;
            int rowIdx = tmNum - 1;
            if (rowIdx < 0 || rowIdx >= dgvTM.Rows.Count) continue;

            // Try to match move by name first, then by index
            int moveIdx = Array.IndexOf(movelist, movePart);
            if (moveIdx < 0 && int.TryParse(movePart, out int moveId) && moveId >= 0 && moveId < movelist.Length)
                moveIdx = moveId;
            if (moveIdx < 0) continue;

            dgvTM.Rows[rowIdx].Cells[2].Value = movelist[moveIdx];
            updated++;
        }
        WinFormsUtil.Alert($"Imported {updated} TM entries!");
    }

    private void B_UpdateDesc_Click(object sender, EventArgs e)
    {
        const string disclaimer = "Warning: This will overwrite ALL TM item descriptions in the game text with the descriptions of the moves they currently teach.\n\n" +
                                   "This action cannot be undone. Are you sure you want to proceed?";
        
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, disclaimer) != DialogResult.Yes)
            return;

        List<ushort> tms = [];
        List<ushort> items = [];
        for (int i = 0; i < dgvTM.Rows.Count; i++)
            currentTMs.Add((ushort)Array.IndexOf(movelist, dgvTM.Rows[i].Cells[1].Value));

        ushort[] tmlist = [.. tms];
        ushort[] itemlist = [.. items];

        // Sync move descriptions into item descriptions (same logic as SetList)
        string[] itemDescriptions = Main.Config.GetText(TextName.ItemFlavor);
        string[] moveDescriptions = Main.Config.GetText(TextName.MoveFlavor);
        for (int i = 0; i < tmlist.Length; i++)
        {
            int targetID = itemlist[i];
            if (targetID > 0 && targetID < itemDescriptions.Length)
                itemDescriptions[targetID] = moveDescriptions[tmlist[i]];
        }
        Main.Config.SetText(TextName.ItemFlavor, itemDescriptions);
        Main.Config.SaveText(TextName.ItemFlavor);

        WinFormsUtil.Alert("TM item descriptions updated to match current moves!");
    }

    private void TB_Offset_TextChanged(object sender, EventArgs e)
    {
        if (uint.TryParse(TB_Offset.Text, System.Globalization.NumberStyles.HexNumber, null, out uint res))
        {
            offset = (int)res;
            GetList();
        }
    }
}