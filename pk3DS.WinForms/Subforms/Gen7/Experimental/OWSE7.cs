using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Windows.Forms;

using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms
{
    public partial class OWSE7 : Form
    {
        private readonly LazyGARCFile EncounterData;
        private readonly LazyGARCFile ZoneData;
        private readonly LazyGARCFile WorldData;

        private readonly ByteViewer byteViewer;
        //private readonly LazyGARCFile WorldData;
        // private readonly LazyGARCFile ZoneData;
        private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);

        public OWSE7(LazyGARCFile ed, LazyGARCFile zd, LazyGARCFile wd)
        {
            EncounterData = ed;
            ZoneData = zd;
            WorldData = wd;

            locationList = Main.Config.GetText(TextName.metlist_000000);
            locationList = SMWE.GetGoodLocationList(locationList);

            InitializeComponent();
            
            byteViewer = new ByteViewer();
            byteViewer.Location = new System.Drawing.Point(226, 6);
            byteViewer.Size = new System.Drawing.Size(400, 428);
            byteViewer.Dock = DockStyle.Fill;
            byteViewer.SetDisplayMode(DisplayMode.Hexdump);
            hexPanel.Controls.Add(byteViewer);
            
            WinFormsUtil.ApplyTheme(this);

            SetupDGV();
            LoadData();
        }

        private readonly string[] locationList;

        private void LoadData()
        {
            var worlds = WorldData.Files.Select(f => Mini.UnpackMini(f, "WD")[0]).ToArray();
            byte[][] zdfiles = ZoneData.Files;
            var worldData = zdfiles[1];
            var zoneData = zdfiles[0];
            var zones = ZoneData7.GetZoneData7Array(zoneData, worldData, locationList, worlds);

            var areas = new string[EncounterData.FileCount / 11];
            for (int i = 0; i < areas.Length; ++i)
            {
                var names = String.Join(",", zones.Where(z => z.AreaIndex == i).Select((z) => locationList[z.ParentMap]));
                areas[i] = $"{i:000} {names}";
            }
            CB_LocationID.Items.AddRange(areas);
            CB_LocationID.SelectedIndex = 0;
        }

        private void CB_LocationID_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetEntry();
            entry = CB_LocationID.SelectedIndex;
            GetEntry();
        }

        private int entry = -1;

        private void SetEntry()
        {
            if (entry < 0)
                return;

            Console.WriteLine($"Setting {CB_LocationID.Text}");
            // research only, no set
        }

        private bool loading;
        private World Map;

        private void GetEntry()
        {
            Console.WriteLine($"Loading {CB_LocationID.Text}");
            int index = entry * 11;
            // 00 - ED (???)
            // 01 - BG (???)
            // 02 - TR (???)
            // 03 - AC (???)
            // 04 - AS (???)
            // 05 - ???
            // 06 - AE (Area Environment)
            // 07 - ZS (Zone Script)
            // 08 - ZI (Zone Info)
            // 09 - EA (Encounter Area)
            // 10 - BG (???)

            if (index > EncounterData.FileCount)
            {
                Console.WriteLine("Out of range.");
                tabControl1.Visible = false;
                return;
            }
            tabControl1.Visible = true;

            Map = new World(EncounterData, entry);
            loading = true;

            NUD_7_Count.Maximum = Map.ZoneScripts.Length;
            NUD_7_Count.Value = Math.Min(Map.ZoneScripts.Length, 1);

            NUD_8_Count.Maximum = Map.ZoneInfoScripts.Length;
            NUD_8_Count.Value = Math.Min(Map.ZoneInfoScripts.Length, 1);
            loading = false;

            LoadDGV();
            LoadTree();
            LoadTrainers();
            NUD_7_Count_ValueChanged(NUD_7_Count, null);
            NUD_8_Count_ValueChanged(NUD_8_Count, null);
        }

        private class World
        {
            private readonly byte[][] _7;
            private readonly byte[][] _8;
            private readonly byte[][] _envData;
            private readonly byte[][] _itemDataFull;

            private bool HasZS => _7 != null;
            private bool HasZI => _8 != null;
            public readonly Script[] ZoneScripts;
            public readonly Script[] ZoneInfoScripts;
            public List<int> Items;
            public OWTrainerFile TrainerData;
            public int TrainerDataIndex = -1;

            public World(LazyGARCFile garc, int worldID)
            {
                int index = worldID * 11;
                _7 = Mini.UnpackMini(garc[index + 7], "ZS");
                _8 = Mini.UnpackMini(garc[index + 8], "ZI");


                ZoneScripts = HasZS ? _7.Select(arr => new Script(arr)).ToArray() : Array.Empty<Script>();
                ZoneInfoScripts = HasZI ? _8.Select(arr => new Script(arr)).ToArray() : Array.Empty<Script>();

                _envData = Mini.UnpackMini(garc[index], "ED");
                _itemDataFull = Mini.UnpackMini(_envData[10], "EI");
                List<int> items = new List<int>();
                foreach (var itemData in _itemDataFull)
                {
                    if (itemData.Length <= 0) continue;
                    int count = itemData[0];
                    for (int i = 0; i < count; ++i)
                    {
                        items.Add(BitConverter.ToInt16(itemData, i * 64 + 52));
                    }
                }
                Items = items;

                for (int i = 0; i < _envData.Length; i++)
                {
                    if (_envData[i].Length > 2 && _envData[i][0] == 0x45 && _envData[i][1] == 0x54) // 'E' 'T'
                    {
                        TrainerDataIndex = i;
                        TrainerData = new OWTrainerFile(_envData[i]);
                        break; // We only want the first ET file
                    }
                }
            }

            public void WriteItems(LazyGARCFile garc, int worldID)
            {
                int listId = 0;
                foreach (var itemData in _itemDataFull)
                {
                    if (itemData.Length <= 0) continue;
                    int count = itemData[0];
                    for (int i = 0; i < count; ++i)
                    {
                        var bytes = BitConverter.GetBytes((short)Items[listId]);
                        int dataId = i * 64 + 52;
                        itemData[dataId] = bytes[0];
                        itemData[dataId + 1] = bytes[1];
                        listId += 1;
                    }
                }
                _envData[10] = Mini.PackMini(_itemDataFull, "EI");
                garc[worldID * 11] = Mini.PackMini(_envData, "ED");
                garc.Save();
            }

            public void WriteTrainers(LazyGARCFile garc, int worldID)
            {
                if (TrainerDataIndex != -1 && TrainerData != null)
                {
                    _envData[TrainerDataIndex] = TrainerData.Write();
                    garc[worldID * 11] = Mini.PackMini(_envData, "ED");
                    garc.Save();
                }
            }
        }

        private void SetupDGV()
        {
            foreach (string t in itemlist)
                dgvItem.Items.Add(t);

            dgvTrainers.Columns.Clear();
            dgvTrainers.Columns.Add("Index", "#");
            dgvTrainers.Columns.Add("Type", "Type");
            dgvTrainers.Columns.Add("X", "X");
            dgvTrainers.Columns.Add("Y", "Y");
            dgvTrainers.Columns.Add("Z", "Z");
            dgvTrainers.Columns.Add("Model", "Model ID");
            dgvTrainers.Columns.Add("BattleID", "Battle Script ID");
            dgvTrainers.Columns.Add("EventID", "Event ID");
            dgvTrainers.Columns.Add("Name", "Trainer Name");
            
            dgvTrainers.Columns[0].ReadOnly = true;
            dgvTrainers.Columns[1].ReadOnly = true;
            dgvTrainers.Columns[8].ReadOnly = true;
        }

        private void LoadDGV()
        {
            dgv.Rows.Clear();
            foreach (int item in Map.Items)
            {
                dgv.Rows.Add();
                dgv.Rows[dgv.Rows.Count - 1].Cells[0].Value = dgv.Rows.Count - 1;

                int index = item > 0 ? item : 1;
                dgv.Rows[dgv.Rows.Count - 1].Cells[1].Value = itemlist[index];
            }
        }

        record struct Entry(string name, int index, byte[] data)
        {
            public bool IsMini { get => name.All(c => c >= 'A' && c <= 'Z'); }
        }
        Dictionary<TreeNode, Entry> ENodeMap = new Dictionary<TreeNode, Entry>();

        private TreeNode createNode(byte[] data, int index)
        {
            string name = "";
            if (data.Length > 0 && data[0] >= 'A' && data[0] <= 'Z')
            {
                name = new string(new char[] { (char)data[0], (char)data[1] });
            }
            else
            {
                name = index.ToString();
            }

            var node = new TreeNode(name);
            ENodeMap[node] = new Entry { name = name, index = index, data = data };
            return node;
        }

        private void LoadTree()
        {
            treeView1.Nodes.Clear();
            ENodeMap.Clear();
            for (int i = 0; i < 11; ++i)
            {
                var node = createNode(EncounterData[entry * 11 + i], i);
                treeView1.Nodes.Add(node);
            }
            treeView1.SelectedNode = treeView1.Nodes[0];
        }
        private void SaveNode(TreeNode node)
        {
            while (node != null)
            {
                if (!ENodeMap[node].IsMini) {
                    node = node.Parent;
                    continue;
                }

                var newDatas = node.Nodes.Cast<TreeNode>().Select(n => ENodeMap[n].data).ToArray();
                ENodeMap[node] = ENodeMap[node] with { data = Mini.PackMini(newDatas, ENodeMap[node].name) };

                var parent = node.Parent;
                if (parent == null)
                {
                    EncounterData[entry * 11 + ENodeMap[node].index] = ENodeMap[node].data;
                }
                node = parent;
            }
            EncounterData.Save();
        }

        private void BuildNode(TreeNode node)
        {
            node.Nodes.Clear();
            var selectedData = ENodeMap[node];
            if (selectedData.IsMini)
            {
                var unpacked = Mini.UnpackMini(selectedData.data, selectedData.name);
                int i = 0;
                foreach (byte[] data in unpacked)
                {
                    var newNode = createNode(data, i++);
                    node.Nodes.Add(newNode);
                }
            }
        }


        private void treeView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            BuildNode(treeView1.SelectedNode);
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var selectedNode = treeView1.SelectedNode;
            var selectedData = ENodeMap[selectedNode];
            byteViewer.SetBytes(selectedData.data);
            byteViewer.SetStartLine(0);
            path_label.Text = selectedNode.FullPath;
        }

        private void button_Add_Click(object sender, EventArgs e)
        {
            var selectedNode = treeView1.SelectedNode;
            var data = ENodeMap[selectedNode];
            if (!data.IsMini) return;
            int index = selectedNode.Nodes.Count - 1;
            var newEntry = new Entry(index.ToString(), index, new byte[] { });

            var newNode = new TreeNode(newEntry.name);
            ENodeMap[newNode] = newEntry;
            selectedNode.Nodes.Add(newNode);
            SaveNode(newNode);
        }

        private void button_Remove_Click(object sender, EventArgs e)
        {
            var selectedNode = treeView1.SelectedNode;
            if (selectedNode.Parent == null) return;
            var parent = selectedNode.Parent;
            parent.Nodes.Remove(selectedNode);
            SaveNode(parent);
            BuildNode(parent);
        }

        private void button_Import_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();
            if (result != DialogResult.OK) return;

            var newData = System.IO.File.ReadAllBytes(dialog.FileName);
            var node = treeView1.SelectedNode;
            ENodeMap[node] = ENodeMap[node] with { data = newData };
            BuildNode(node);
            SaveNode(node);
        }


        private void button_Export_Click(object sender, EventArgs e)
        {
            var dialog = new SaveFileDialog { FileName = CB_LocationID.Text + treeView1.SelectedNode.FullPath.Replace('\\', '.') };
            var result = dialog.ShowDialog();
            if (result != DialogResult.OK) return;
            System.IO.File.WriteAllBytes(dialog.FileName, ENodeMap[treeView1.SelectedNode].data);
        }


        private void button_dump_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result != DialogResult.OK) return;
            byte[] bytes = new byte[] { };
            for (int i = 0; i < 11; ++i)
            {
                var entry = ENodeMap[treeView1.Nodes[i]];
                System.IO.File.WriteAllBytes(dialog.SelectedPath + "/" + entry.name, entry.data);
            }
        }

        private void NUD_7_Count_ValueChanged(object sender, EventArgs e)
        {
            if (loading)
                return;

            bool vis = ((sender as NumericUpDown)?.Value ?? 0) != 0;
            RTB_7_Raw.Visible = RTB_7_Script.Visible = L_7_Info.Visible = RTB_7_Parse.Visible = vis;
            if (!vis)
                return;

            var script = Map.ZoneScripts[(int)NUD_7_Count.Value - 1];
            L_7_Count.Text = $"Files: {Map.ZoneScripts.Length}";
            RTB_7_Raw.Lines = Scripts.GetHexLines(script.Raw, 16);
            RTB_7_Script.Lines = Scripts.GetHexLines(script.DecompressedInstructions);
            RTB_7_Parse.Lines = script.ParseScript;

            string[] lines =
            {
                "Commands:" + Environment.NewLine + RTB_7_Script.Lines.Length,
                "CBytes:" + Environment.NewLine + script.CompressedBytes.Length,
            };
            L_7_Info.Text = string.Join(Environment.NewLine, lines);
        }

        private void NUD_8_Count_ValueChanged(object sender, EventArgs e)
        {
            if (loading)
                return;

            bool vis = ((sender as NumericUpDown)?.Value ?? 0) != 0;
            RTB_8_Raw.Visible = RTB_8_Script.Visible = L_8_Info.Visible = RTB_8_Parse.Visible = vis;
            if (!vis)
                return;

            var script = Map.ZoneInfoScripts[(int)NUD_8_Count.Value - 1];
            L_8_Count.Text = $"Files: {Map.ZoneInfoScripts.Length}";
            RTB_8_Raw.Lines = Scripts.GetHexLines(script.Raw, 16);
            RTB_8_Script.Lines = Scripts.GetHexLines(script.DecompressedInstructions);
            RTB_8_Parse.Lines = script.ParseScript;

            string[] lines =
            {
                "Commands:" + Environment.NewLine + RTB_8_Script.Lines.Length,
                "CBytes:" + Environment.NewLine + script.CompressedBytes.Length,
            };
            L_8_Info.Text = string.Join(Environment.NewLine, lines);
        }


        private void buttonSave_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dgv.Rows.Count; ++i)
            {
                Map.Items[i] = Array.IndexOf(itemlist, dgv.Rows[i].Cells[1].Value);
            }
            Map.WriteItems(EncounterData, entry);
            if (Map.TrainerDataIndex != -1)
                Map.WriteTrainers(EncounterData, entry);
            SetEntry();
        }

        private void LoadTrainers()
        {
            dgvTrainers.Rows.Clear();
            CB_UnusedTrainer.Items.Clear();

            if (Map == null || Map.TrainerDataIndex == -1 || Map.TrainerData == null)
            {
                tab_Trainers.Enabled = false;
                return;
            }

            tab_Trainers.Enabled = true;
            int areaCount = Map.TrainerData.AreaCount;
            NUD_Area.Maximum = Math.Max(0, areaCount - 1);
            NUD_Area.Minimum = 0;
            NUD_Area.Value = 0;

            PopulateTrainerArea(0);
        }

        private void PopulateTrainerArea(int area)
        {
            dgvTrainers.Rows.Clear();
            CB_UnusedTrainer.Items.Clear();

            if (Map.TrainerData == null || area >= Map.TrainerData.Areas.Count)
                return;

            var entries = Map.TrainerData.Areas[area];
            if (entries.Count == 0)
            {
                MessageBox.Show("This area has no trainers!");
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var tr = entries[i];
                string name = "";
                if (tr.BattleScriptID >= 1000)
                {
                    int tId = (int)(tr.BattleScriptID - 1000);
                    var trNames = Main.Config.GetText(TextName.TrainerNames);
                    if (tId >= 0 && tId < trNames.Length)
                        name = trNames[tId];
                }

                dgvTrainers.Rows.Add(
                    i.ToString(),
                    tr.ObjectType.ToString("X2"),
                    tr.X.ToString(),
                    tr.Y.ToString(),
                    tr.Z.ToString(),
                    tr.ModelID.ToString(),
                    tr.BattleScriptID.ToString(),
                    tr.EventID.ToString(),
                    name
                );

                if (tr.ObjectType == 0x07)
                {
                    CB_UnusedTrainer.Items.Add($"[{i}] Model: {tr.ModelID}, Event: {tr.EventID}, Battle: {tr.BattleScriptID}");
                }
            }

            if (CB_UnusedTrainer.Items.Count > 0)
                CB_UnusedTrainer.SelectedIndex = 0;
        }

        private void NUD_Area_ValueChanged(object sender, EventArgs e)
        {
            PopulateTrainerArea((int)NUD_Area.Value);
        }

        private void B_ConvertTrainer_Click(object sender, EventArgs e)
        {
            if (dgvTrainers.CurrentRow == null) return;
            if (CB_UnusedTrainer.SelectedIndex < 0) return;

            int npcIndex = dgvTrainers.CurrentRow.Index;
            string selectedTrainerStr = CB_UnusedTrainer.SelectedItem.ToString();
            int bracketClose = selectedTrainerStr.IndexOf(']');
            if (bracketClose < 0) return;
            if (!int.TryParse(selectedTrainerStr.Substring(1, bracketClose - 1), out int trIndex)) return;

            if (npcIndex == trIndex)
            {
                MessageBox.Show("Cannot swap an entry with itself.");
                return;
            }

            int area = (int)NUD_Area.Value;
            var entries = Map.TrainerData.Areas[area];
            var npc = entries[npcIndex];
            var tr = entries[trIndex];

            uint tempEvent = npc.EventID;
            npc.EventID = tr.EventID;
            tr.EventID = tempEvent;

            uint tempBattle = npc.BattleScriptID;
            npc.BattleScriptID = tr.BattleScriptID;
            tr.BattleScriptID = tempBattle;

            uint tempObj = npc.ObjectType;
            npc.ObjectType = tr.ObjectType;
            tr.ObjectType = tempObj;

            PopulateTrainerArea(area);
            MessageBox.Show("NPC successfully converted to a Trainer using the chosen unused trainer data. Model ID was preserved.");
        }

        private void B_SaveTrainers_Click(object sender, EventArgs e)
        {
            int area = (int)NUD_Area.Value;
            if (Map.TrainerData != null && area < Map.TrainerData.Areas.Count)
            {
                var entries = Map.TrainerData.Areas[area];
                for (int i = 0; i < entries.Count; i++)
                {
                    var row = dgvTrainers.Rows[i];
                    var tr = entries[i];

                    if (float.TryParse(row.Cells[2].Value?.ToString(), out float x)) tr.X = x;
                    if (float.TryParse(row.Cells[3].Value?.ToString(), out float y)) tr.Y = y;
                    if (float.TryParse(row.Cells[4].Value?.ToString(), out float z)) tr.Z = z;
                    
                    if (uint.TryParse(row.Cells[5].Value?.ToString(), out uint model)) tr.ModelID = model;
                    if (uint.TryParse(row.Cells[6].Value?.ToString(), out uint battle)) tr.BattleScriptID = battle;
                    if (uint.TryParse(row.Cells[7].Value?.ToString(), out uint ev)) tr.EventID = ev;
                }
            }

            Map.WriteTrainers(EncounterData, entry);
            MessageBox.Show("Trainer Data Saved.");
        }

    }
}