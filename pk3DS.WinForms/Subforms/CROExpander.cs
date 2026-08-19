using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Modding;


namespace pk3DS.WinForms
{
    public partial class CROExpander : Form
    {
        private byte[] Data;
        private string LoadedPath;

        public CROExpander()
        {
            InitializeComponent();
            PopulateCROList();
            CB_SearchMethod.SelectedIndex = 0;
        }

        private void PopulateCROList()
        {
            CB_CRO.Items.Clear();
            if (string.IsNullOrEmpty(Main.RomFSPath) || !Directory.Exists(Main.RomFSPath))
                return;

            var croFiles = Directory.GetFiles(Main.RomFSPath, "*.cro", SearchOption.TopDirectoryOnly);
            foreach (var file in croFiles)
                CB_CRO.Items.Add(Path.GetFileName(file));

            if (CB_CRO.Items.Count > 0)
                CB_CRO.SelectedIndex = 0;
        }

        private void B_Browse_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "CRO files|*.cro|All files|*.*" };
            if (ofd.ShowDialog() != DialogResult.OK) return;
            LoadCRO(ofd.FileName);
        }

        private void CB_CRO_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CB_CRO.SelectedIndex < 0) return;
            string path = Path.Combine(Main.RomFSPath, CB_CRO.SelectedItem.ToString());
            LoadCRO(path);
        }

        private void LoadCRO(string path)
        {
            if (!File.Exists(path)) return;
            Data = File.ReadAllBytes(path);
            LoadedPath = path;
            bool expanded = ResearchEngine.IsFileExpanded(path, Data);
            string stateStr = expanded ? " [Expanded Mod Active]" : "";
            L_Status.Text = $"Loaded: {Path.GetFileName(path)} ({Data.Length:X} bytes){stateStr}";
        }


        #region CRO Logic (Using CROUtil)

        private uint ReadU32(int offset) => CROUtil.ReadU32(Data, offset);
        private void WriteU32(uint value, int offset) => CROUtil.WriteU32(Data, value, offset);

        private void ExpandSegment(char section, int bytesToAdd)
        {
            if (Data == null) return;
            Data = CROUtil.ExpandSegment(Data, section, bytesToAdd);
            WinFormsUtil.Alert("Expansion complete!", $"Added {bytesToAdd:X} bytes to section '{section}'.");
            LoadCROFromMemory();
        }

        private void LoadCROFromMemory()
        {
            L_Status.Text = $"Modified: {Path.GetFileName(LoadedPath)} ({Data.Length:X} bytes)";
        }

        private void B_ApplyExpand_Click(object sender, EventArgs e)
        {
            if (Data == null) return;
            char section = ' ';
            if (RB_Code.Checked) section = 'c';
            else if (RB_Data.Checked) section = 'd';
            else if (RB_BSS.Checked) section = 'b';
            else if (RB_Reloc.Checked) section = 'r';

            int amount = (int)NUD_Pages.Value * 0x1000;
            if (section == 'b') amount = (int)NUD_Pages.Value * 4; 

            ExpandSegment(section, amount);
        }

        private void RepointExpand(bool isTable)
        {
            if (Data == null) return;
            // 1. Get offsets
            uint segmentTableOffset = ReadU32(0xC8);
            uint[] startTable = new uint[4];
            for (int i = 0; i < 4; i++)
                startTable[i] = ReadU32((int)(segmentTableOffset + i * 0x0C));

            uint patchTableOffset = ReadU32(0x128);
            uint patchTableCount = ReadU32(0x12C);

            // 2. Find the target
            uint findValue = (uint)NUD_FindAddr.Value;
            bool searchByWriteLocation = CB_SearchMethod.SelectedIndex == 0; // 0 = Written To, 1 = Absolute Address

            int targetSegment = -1;
            uint targetAddend = 0;

            for (int i = 0; i < patchTableCount; i++)
            {
                int entryOfs = (int)(patchTableOffset + i * 0x0C);
                uint writeInfo = ReadU32(entryOfs);
                uint addend = ReadU32(entryOfs + 8);
                int seg = Data[entryOfs + 5];

                if (searchByWriteLocation)
                {
                    uint writeAddr = (writeInfo >> 4) + startTable[writeInfo & 0xF];
                    if (writeAddr == findValue)
                    {
                        targetSegment = seg;
                        targetAddend = addend;
                        break;
                    }
                }
                else
                {
                    uint targetAddr = addend + startTable[seg];
                    if (targetAddr == findValue)
                    {
                        targetSegment = seg;
                        targetAddend = addend;
                        break;
                    }
                }
            }

            if (targetSegment == -1)
            {
                WinFormsUtil.Error("No reference found in the relocation table for that address.");
                return;
            }

            uint updateValue = (uint)NUD_NewAddr.Value;

            if (isTable)
            {
                uint tableLen = (uint)NUD_TableLen.Value;
                uint oldTableAbsolute = targetAddend + startTable[targetSegment];

                // Check for unused space (Python optimization)
                while (updateValue + tableLen > Data.Length)
                {
                    if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Target space is outside current file. Expand .data?"))
                        return;
                    ExpandSegment('d', 0x1000);
                }

                // Copy Data
                for (int i = 0; i < tableLen; i++)
                {
                    Data[updateValue + i] = Data[oldTableAbsolute + i];
                    Data[oldTableAbsolute + i] = 0xCC; // Blank out old
                }

                // Get segment of new location
                int updateSegment = 2; // Default to .data
                uint codeLen = ReadU32((int)segmentTableOffset + 4);
                uint dataStart = ReadU32((int)segmentTableOffset + 0x18);
                if (updateValue < startTable[0] + codeLen) updateSegment = 0;
                else if (updateValue < dataStart) updateSegment = 1;

                // Update Relocations
                for (int i = 0; i < patchTableCount; i++)
                {
                    int entryOfs = (int)(patchTableOffset + i * 0x0C);
                    uint writeInfo = ReadU32(entryOfs);
                    uint pointedAt = ReadU32(entryOfs + 8);
                    int seg = Data[entryOfs + 5];

                    // check if points AT an address in our table
                    if (seg == targetSegment && pointedAt >= targetAddend && pointedAt < targetAddend + tableLen)
                    {
                        Data[entryOfs + 5] = (byte)updateSegment;
                        CROUtil.WriteU32(Data, updateValue - startTable[updateSegment] + (pointedAt - targetAddend), entryOfs + 8);
                    }

                    // writes TO an address in our table
                    uint writeOffset = writeInfo >> 4;
                    if (writeOffset >= targetAddend && writeOffset < targetAddend + tableLen)
                    {
                        uint newWriteOffset = updateValue + (writeOffset - targetAddend) - startTable[updateSegment];
                        CROUtil.WriteU32(Data, (newWriteOffset << 4) | (uint)updateSegment, entryOfs);
                    }
                }
                WinFormsUtil.Alert("Table moved and references updated!");
            }
            else // Function
            {
                int count = 0;
                for (int i = 0; i < patchTableCount; i++)
                {
                    int entryOfs = (int)(patchTableOffset + i * 0x0C);
                    if (ReadU32(entryOfs + 8) == targetAddend && Data[entryOfs + 5] == targetSegment)
                    {
                        // Get segment of writing location
                        uint writingInfo = ReadU32(entryOfs);
                        int writeSeg = (int)(writingInfo & 0xF);
                        CROUtil.WriteU32(Data, updateValue - startTable[writeSeg], entryOfs + 8);
                        count++;
                    }
                }
                WinFormsUtil.Alert("Function repointed!", $"Updated {count} references.");
            }
            LoadCROFromMemory();
        }

        private void B_ApplyTable_Click(object sender, EventArgs e) => RepointExpand(true);
        private void B_ApplyFunc_Click(object sender, EventArgs e) => RepointExpand(false);

        private void B_Save_Click(object sender, EventArgs e)
        {
            if (Data == null) return;
            string bakPath = LoadedPath + ".bak";
            if (!File.Exists(bakPath)) File.Copy(LoadedPath, bakPath);
            File.WriteAllBytes(LoadedPath, Data);
            WinFormsUtil.Alert("Saved!", LoadedPath + "\n\nIMPORTANT: Please reload your ROM in pk3DS (File -> Open) so all editors update to the new expanded file sizes and prevent crashes.");
        }

        #endregion
    }
}
