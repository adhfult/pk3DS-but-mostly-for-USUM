using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;
using pk3DS.Core.Structures;
using pk3DS.Core;
using pk3DS.Core.Randomizers;
using pk3DS.WinForms.Properties;

namespace pk3DS.WinForms;

public partial class EggMoveEditor7 : Form
{
    public EggMoveEditor7(byte[][] infiles)
    {
        InitializeComponent();
        files = infiles;
        string[] species = Main.Config.GetText(TextName.SpeciesNames);
        string[][] AltForms = Main.Config.Personal.GetFormList(species, Main.Config.MaxSpeciesID);
string[] specieslist = Main.Config.Personal.GetPersonalEntryList(AltForms, species, Main.Config.MaxSpeciesID, out baseForms, out formVal);
        specieslist[0] = movelist[0] = "";

        SetupDGV();
        entries = infiles.Select(z => new EggMoves7(z)).ToArray();
        string[] names = new string[entries.Length];
        dgv.CellValueChanged += UpdateCounters;
        dgv.RowsAdded += UpdateCounters;
        dgv.RowsRemoved += UpdateCounters;

        for (int i = 0; i <= Main.Config.MaxSpeciesID; i++) // add all base species
        {
            if (i < species.Length) names[i] = species[i];
            int formoff = entries[i].FormTableIndex;
            int count = Main.Config.Personal[i].FormeCount;
            for (int j = 1; j < count; j++)
            {
                if (formoff + j - 1 < names.Length)
                    names[formoff + j - 1] ??= $"{names[i]} [{AltForms[i][j].Replace(names[i] + " ", "")}]";
            }
        }

        var newlist = names.Select((_, i) => new ComboItem { Text = (names[i] ?? "Extra") + $" ({i})", Value = i });
        newlist = newlist.GroupBy(z => z.Text.StartsWith("Extra"))
            .Select(z => z.OrderBy(item => item.Text))
            .SelectMany(z => z).ToList();
        NUD_FormTable.Maximum = files.Length;

        CB_Species.DisplayMember = "Text";
        CB_Species.ValueMember = "Value";
        CB_Species.DataSource = newlist;

        CB_Species.SelectedIndex = 0;
        RandSettings.GetFormSettings(this, groupBox1.Controls);

        vanillaEntries = infiles.Select(z => new EggMoves7((byte[])z.Clone())).ToArray();
        UpdateChangelog();

        Shown += (sender, e) => {
            if (StartSpecies >= 0)
                CB_Species.SelectedValue = StartSpecies;
        };
    }

    public int StartSpecies { get; set; } = -1;

    private readonly EggMoves7[] entries;
    private readonly EggMoves7[] vanillaEntries;

    private readonly byte[][] files;
    private int entry = -1;
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private bool dumping;
    private readonly int[] baseForms, formVal;

    private void SetupDGV()
    {
        string[] sortedmoves = (string[])movelist.Clone();
        Array.Sort(sortedmoves);
        var dgvMove = new DataGridViewComboBoxColumn();
        {
            dgvMove.HeaderText = "Move";
            dgvMove.DisplayIndex = 0;
            for (int i = 0; i < movelist.Length; i++)
                dgvMove.Items.Add(sortedmoves[i]); // add only the Names

            dgvMove.Width = 135;
            dgvMove.FlatStyle = FlatStyle.Flat;
        }
        dgv.Columns.Add(dgvMove);
    }

    private EggMoves pkm = new EggMoves7([]);

    private void GetList()
    {
        entry = WinFormsUtil.GetIndex(CB_Species);
        int s = 0, f = 0;
        if (entry <= Main.Config.MaxSpeciesID)
        {
            s = entry;
        }
        int[] specForm = [s, f];
        string filename = "_" + specForm[0] + (entry > Main.Config.MaxSpeciesID ? "_" + (specForm[1] + 1) : "");
        PB_MonSprite.Image = (Bitmap)Resources.ResourceManager.GetObject(filename);

        dgv.Rows.Clear();
        pkm = entries[entry];
        NUD_FormTable.Value = pkm.FormTableIndex;
        if (pkm.Count < 1) { files[entry] = []; return; }
        dgv.Rows.Add(pkm.Count);

        // Fill Entries
        for (int i = 0; i < pkm.Count; i++)
            dgv.Rows[i].Cells[0].Value = movelist[pkm.Moves[i]];

        dgv.CancelEdit();
    }

    private void SetList()
    {
        if (entry < 1 || dumping) return;
        List<int> moves = [];
        for (int i = 0; i < dgv.Rows.Count - 1; i++)
        {
            int move = Array.IndexOf(movelist, dgv.Rows[i].Cells[0].Value);
            if (move > 0 && !moves.Contains((ushort)move)) moves.Add(move);
        }
        pkm.Moves = [.. moves];
        pkm.FormTableIndex = (int)NUD_FormTable.Value;

        entries[entry] = (EggMoves7)pkm;
    }

    private void ChangeEntry(object sender, EventArgs e)
    {
        SetList();
        GetList();
        UpdateChangelog();
    }

    private void UpdateChangelog()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Egg Move Changes ===");
        for (int i = 0; i < entries.Length; i++)
        {
            var cur = entries[i];
            var van = vanillaEntries[i];
            if (cur.Moves.SequenceEqual(van.Moves)) continue;

            string name = CB_Species.Items.Cast<ComboItem>().FirstOrDefault(z => (int)z.Value == i)?.Text ?? $"Index {i}";
            sb.AppendLine($"\n[{name}]");
            
            var added = cur.Moves.Except(van.Moves).ToList();
            var removed = van.Moves.Except(cur.Moves).ToList();

            foreach (var m in added) sb.AppendLine($"+ {movelist[m]}");
            foreach (var m in removed) sb.AppendLine($"- {movelist[m]}");
        }
        RTB_Changelog.Text = sb.ToString();
    }
private static Dictionary<string, int> formMappingCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly string mapFilePath = Path.Combine(Application.StartupPath, "custom_form_mappings.txt");

    private void LoadMappingCache()
    {
        if (!File.Exists(mapFilePath)) return;
        foreach (string line in File.ReadAllLines(mapFilePath))
        {
            var parts = line.Split('=');
            if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                formMappingCache[parts[0]] = id;
        }
    }

    private void SaveMappingCache()
    {
        var lines = formMappingCache.Select(kvp => $"{kvp.Key}={kvp.Value}");
        File.WriteAllLines(mapFilePath, lines);
    }

private int PromptFormMapping(string formName)
    {
        Form prompt = new Form()
        {
            Width = 420, Height = 210, FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Map Custom Form", StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false
        };
        
        Label textLabel = new Label() { 
            Left = 15, Top = 15, Width = 380, Height = 35, 
            Text = $"Unrecognized form '{formName}'.\nSelect the Pokémon this form corresponds to:" 
        };
        
        ComboBox cb = new ComboBox() { 
            Left = 15, Top = 55, Width = 230, 
            DropDownStyle = ComboBoxStyle.DropDown,
            DisplayMember = "Text", ValueMember = "Value",
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };
        
        // FIX: Add items directly to bypass deferred DataSource binding crashes
        foreach (ComboItem ci in CB_Species.Items) cb.Items.Add(ci);
        
        PictureBox pb = new PictureBox() { 
            Left = 260, Top = 55, Width = 120, Height = 100, 
            SizeMode = PictureBoxSizeMode.CenterImage, BorderStyle = BorderStyle.FixedSingle 
        };
        
        cb.SelectedIndexChanged += (sender, e) => {
            var ci = cb.SelectedItem as ComboItem;
            if (ci != null && (int)ci.Value > 0) {
                int id = (int)ci.Value;
                int s = id <= Main.Config.MaxSpeciesID ? id : baseForms[id];
                int f = id <= Main.Config.MaxSpeciesID ? 0 : formVal[id];
                
                var rawImg = WinFormsUtil.GetSprite(s, f, 0, 0, Main.Config);
                var bigImg = new Bitmap(rawImg.Width * 2, rawImg.Height * 2);
                for (int x = 0; x < rawImg.Width; x++)
                for (int y = 0; y < rawImg.Height; y++)
                {
                    Color c = rawImg.GetPixel(x, y);
                    bigImg.SetPixel(2 * x, 2 * y, c);
                    bigImg.SetPixel((2 * x) + 1, 2 * y, c);
                    bigImg.SetPixel(2 * x, (2 * y) + 1, c);
                    bigImg.SetPixel((2 * x) + 1, (2 * y) + 1, c);
                }
                pb.Image = bigImg;
            } else {
                pb.Image = null;
            }
        };
        
        Button confirmation = new Button() { Text = "Map Form", Left = 15, Width = 110, Top = 100, DialogResult = DialogResult.OK };
        Button cancel = new Button() { Text = "Skip", Left = 135, Width = 110, Top = 100, DialogResult = DialogResult.Cancel };
        
        prompt.Controls.Add(cb);
        prompt.Controls.Add(pb);
        prompt.Controls.Add(confirmation);
        prompt.Controls.Add(cancel);
        prompt.Controls.Add(textLabel);
        prompt.AcceptButton = confirmation;
        prompt.CancelButton = cancel;
        
        // Safety check using the natively populated cb.Items count
        if (cb.Items.Count > 0)
        {
            cb.SelectedIndex = cb.Items.Count > 1 ? 1 : 0; 
        }
        
        return prompt.ShowDialog() == DialogResult.OK && cb.SelectedItem != null ? (int)((ComboItem)cb.SelectedItem).Value : -1;
    }
    private void B_RandAll_Click(object sender, EventArgs e)
    {
        var sets = entries;
        var rand = new EggMoveRandomizer(Main.Config, sets)
        {
            Expand = CHK_Expand.Checked,
            ExpandTo = (int)NUD_Moves.Value,
            STAB = CHK_STAB.Checked,
            STABPercent = NUD_STAB.Value,
            BannedMoves = [165, 621, 464, .. Legal.Z_Moves], // Struggle, Hyperspace Fury, Dark Void
        };
        rand.Execute();
        // sets.Select(z => z.Write()).ToArray().CopyTo(files, 0);
        GetList();
        WinFormsUtil.Alert("All Pokémon's Egg Moves have been randomized!", "Press the Dump All button to see the new Egg Moves!");
    }

 private void B_AddMove_Click(object sender, EventArgs e)
    {
        dgv.Rows.Add(movelist[1]); // Pound
    }

    private void B_RemoveMove_Click(object sender, EventArgs e)
    {
        if (dgv.CurrentRow != null && !dgv.CurrentRow.IsNewRow)
            dgv.Rows.Remove(dgv.CurrentRow);
    }

    private void UpdateCounters(object sender, EventArgs e)
    {
        if (entry < 1 || dumping || pkm == null) return;
        int moveCount = 0;
        int stabCount = 0;
        var pkmTypes = Main.SpeciesStat[entry].Types;
        var moveData = Main.Config.Moves;

        for (int i = 0; i < dgv.Rows.Count - 1; i++)
        {
            var cellVal = dgv.Rows[i].Cells[0].Value;
            if (cellVal == null) continue;
            int move = Array.IndexOf(movelist, cellVal);
            if (move > 0)
            {
                moveCount++;
                if (pkmTypes.Contains(moveData[move].Type))
                    stabCount++;
            }
        }
        if (L_TotalMoves != null) L_TotalMoves.Text = $"Total Moves: {moveCount}";
        if (L_STABCount != null) L_STABCount.Text = $"STAB Moves: {stabCount}";
    }

    private void B_Dump_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Dump all Egg Moves to TSV Text File?")) return;

        dumping = true;
        var lines = new List<string>();
        
        foreach (ComboItem item in CB_Species.Items)
        {
            if ((int)item.Value == 0) continue;
            int targetEntry = (int)item.Value;
            var tempPkm = entries[targetEntry];
            if (tempPkm.Count < 1) continue;
            
            string name = item.Text;
            if (name.Contains("(")) name = name.Substring(0, name.LastIndexOf("(")).Trim();
            
            string line = $"{targetEntry}\t{name}";
            for (int j = 0; j < tempPkm.Count; j++)
                line += $"\t{movelist[tempPkm.Moves[j]]}";
                
            lines.Add(line);
        }
        var sfd = new SaveFileDialog { FileName = "EggMoves_TSV.txt", Filter = "Text File|*.txt" };
        SystemSounds.Asterisk.Play();
        if (sfd.ShowDialog() == DialogResult.OK) File.WriteAllLines(sfd.FileName, lines, Encoding.Unicode);
        dumping = false;
    }

    private void B_Import_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        string[] lines = File.ReadAllLines(ofd.FileName);
        int count = 0;

        foreach (string line in lines)
        {
            string[] parts = line.Split('\t');
            if (parts.Length < 2) continue;

            if (int.TryParse(parts[0], out int targetId) && targetId > 0 && targetId < entries.Length)
            {
                List<int> newMoves = new List<int>();
                for (int i = 2; i < parts.Length; i++)
                {
                    string moveName = parts[i].Trim();
                    int moveId = Array.IndexOf(movelist, moveName);
                    if (moveId > 0 && !newMoves.Contains(moveId)) newMoves.Add(moveId);
                }
                entries[targetId].Moves = newMoves.ToArray();
                count++;
            }
        }
        GetList();
        WinFormsUtil.Alert($"Imported native egg moves for {count} Pokémon.");
    }

private void B_ApplyModern_Click(object sender, EventArgs e)
    {
        OpenFileDialog ofd = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        LoadMappingCache(); // Load globally saved forms
        string[] lines = File.ReadAllLines(ofd.FileName);
        int successCount = 0;

        // Take a snapshot of the entries before we start editing them
        EggMoves7[] backupEntries = new EggMoves7[entries.Length];
        for (int i = 0; i < entries.Length; i++) backupEntries[i] = new EggMoves7(entries[i].Write());

        var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (ComboItem item in CB_Species.Items)
        {
            if ((int)item.Value == 0) continue;
            string cleanName = item.Text;
            if (cleanName.Contains("(")) cleanName = cleanName.Substring(0, cleanName.LastIndexOf("(")).Trim();
            if (!nameToId.ContainsKey(cleanName)) nameToId[cleanName] = (int)item.Value;
        }

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split('\t');
            if (parts.Length < 3) continue; 

            string monName = parts[1].Trim();
            int targetId = -1;

            if (nameToId.ContainsKey(monName)) targetId = nameToId[monName];
            else if (formMappingCache.ContainsKey(monName)) targetId = formMappingCache[monName];
            else
            {
                targetId = PromptFormMapping(monName);
                if (targetId == -2) // ABORT ALL trigger
                {
                    if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Import Aborted.", "Do you want to SAVE the progress made so far?\n\nYes = Save partial progress\nNo = Discard and revert everything"))
                    {
                        // Restore snapshot
                        for (int b = 0; b < entries.Length; b++) entries[b] = new EggMoves7(backupEntries[b].Write());
                        WinFormsUtil.Alert("Import aborted. All changes reverted.");
                        GetList();
                        return;
                    }
                    break; // End loop but keep changes
                }
                if (targetId <= 0) continue; // User clicked Skip
                
                formMappingCache[monName] = targetId; 
                SaveMappingCache(); // Save immediately to hard drive
            }

            if (targetId <= 0 || targetId >= entries.Length) continue;

            List<int> newMoves = new List<int>();
            for (int i = 2; i < parts.Length; i++)
            {
                string moveName = parts[i].Trim();
                int moveId = Array.IndexOf(movelist, moveName);
                if (moveId > 0 && !newMoves.Contains(moveId)) newMoves.Add(moveId);
            }
            entries[targetId].Moves = newMoves.ToArray();
            successCount++;
        }
        GetList();
        WinFormsUtil.Alert($"Modern update complete!\nUpdated {successCount} Pokémon Egg Moves.");
    }

    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetList();
        entries.Select(z => z.Write()).ToArray().CopyTo(files, 0);
        RandSettings.SetFormSettings(this, groupBox1.Controls);
    }

    private void B_Goto_Click(object sender, EventArgs e)
    {
        CB_Species.SelectedValue = (int)NUD_FormTable.Value;
    }

    private void B_ImportJSON_Click(object sender, EventArgs e) { /* Placeholder for complex JSON parsing */ WinFormsUtil.Alert("JSON Import logic coming soon!"); }
    private void B_ImportTS_Click(object sender, EventArgs e) { /* Placeholder for TS parsing */ WinFormsUtil.Alert(".TS Import logic coming soon!"); }

    public void CalcStats()
    {
        Move[] MoveData = Main.Config.Moves;
        int movectr = 0;
        int max = 0;
        int spec = 0;
        int stab = 0;
        for (int i = 0; i < Main.Config.MaxSpeciesID; i++)
        {
            byte[] movedata = files[i];
            int movecount = BitConverter.ToUInt16(movedata, 2);
            if (movecount == 65535)
                continue;
            movectr += movecount; // Average Moves
            if (max < movecount) { max = movecount; spec = i; } // Max Moves (and species)
            for (int m = 0; m < movecount; m++)
            {
                int move = BitConverter.ToUInt16(movedata, (m * 2) + 4);
                if (Main.SpeciesStat[i].Types.Contains(MoveData[move].Type))
                    stab++;
            }
        }
        WinFormsUtil.Alert($"Egg Moves: {movectr}\r\nMost Moves: {max} @ {spec}\r\nSTAB Count: {stab}");
    }
}