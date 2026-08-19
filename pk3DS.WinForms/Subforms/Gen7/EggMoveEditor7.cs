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

        // Kept so importers can resolve a species name back to its entry index; this array is
        // indexed the same way "entries" is, including the alternate-form rows.
        entryNames = names;

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

    /// <summary>Species name per entry index, including alternate-form rows.</summary>
    private readonly string[] entryNames;
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
        int s = baseForms[entry];
        int f = formVal[entry];
        if (entry <= Main.Config.MaxSpeciesID)
        {
            s = entry;
            f = 0;
        }
        WinFormsUtil.SetImage(PB_MonSprite, WinFormsUtil.GetSprite(s, f, 0, 0, Main.Config));

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

    // Both of these used to be stubs that only announced themselves. They now read the same two
    // shapes the level-up editor accepts, so egg moves can come from the same sources.
    private void B_ImportJSON_Click(object sender, EventArgs e) => ImportEggMoves(".json");
    private void B_ImportTS_Click(object sender, EventArgs e) => ImportEggMoves(".ts");

    /// <summary>
    /// Imports egg moves from JSON or a Showdown-style listing.
    /// <para>
    /// JSON is <c>{ "Species": ["Move", ...] }</c>. The text form is a species name on its own line
    /// followed by moves prefixed with "-", blocks separated by a blank line - the shape a Poképaste
    /// or a Showdown export already has.
    /// </para>
    /// </summary>
    private void ImportEggMoves(string preferred)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Supported Formats|*.json;*.txt;*.ts;*.tsv|JSON|*.json|Text|*.txt;*.ts;*.tsv",
            FilterIndex = preferred == ".json" ? 2 : 3,
        };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        int count;
        var skipped = new List<string>();
        try
        {
            count = Path.GetExtension(ofd.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? ImportEggJson(File.ReadAllText(ofd.FileName), skipped)
                : ImportEggText(File.ReadAllText(ofd.FileName), skipped);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("The file could not be read.", ex.Message);
            return;
        }

        GetList();

        // Report what did not land as well as what did - a silent partial import here looks exactly
        // like a successful one until the moves are missing in game.
        string detail = skipped.Count == 0
            ? "Every entry was matched."
            : $"Unmatched ({skipped.Count}): " + string.Join(", ", skipped.Take(15))
              + (skipped.Count > 15 ? ", ..." : "");
        WinFormsUtil.Alert($"Imported egg moves for {count} Pokémon.", detail);
    }

    private int ImportEggJson(string json, List<string> skipped)
    {
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
        if (data == null) return 0;

        int count = 0;
        foreach (var kvp in data)
        {
            int id = FindSpeciesId(kvp.Key);
            if (id <= 0) { skipped.Add(kvp.Key); continue; }
            entries[id].Moves = ResolveMoves(kvp.Value, skipped);
            count++;
        }
        return count;
    }

    private int ImportEggText(string text, List<string> skipped)
    {
        int count = 0;
        var blocks = text.Replace("\r\n", "\n").Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            var lines = block.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            if (lines.Length < 2) continue;

            int id = FindSpeciesId(lines[0]);
            if (id <= 0) { skipped.Add(lines[0]); continue; }

            var names = lines.Skip(1)
                             .Where(l => l.StartsWith("-"))
                             .Select(l => l.TrimStart('-').Trim());
            entries[id].Moves = ResolveMoves(names, skipped);
            count++;
        }
        return count;
    }

    private int[] ResolveMoves(IEnumerable<string> names, List<string> skipped)
    {
        var ids = new List<int>();
        foreach (string raw in names)
        {
            string name = raw.Trim();
            if (name.Length == 0) continue;
            int id = Array.FindIndex(movelist, m => string.Equals(m, name, StringComparison.OrdinalIgnoreCase));
            if (id > 0) { if (!ids.Contains(id)) ids.Add(id); }
            else skipped.Add(name);
        }
        return [.. ids];
    }

    /// <summary>Matches a species name case-insensitively, tolerating a trailing form suffix.</summary>
    private int FindSpeciesId(string name)
    {
        string wanted = name.Trim();
        if (wanted.Length == 0) return -1;

        if (entryNames == null) return -1;

        int exact = Array.FindIndex(entryNames, s => string.Equals(s, wanted, StringComparison.OrdinalIgnoreCase));
        if (exact > 0) return exact;

        // "Pikachu-Alola" and the like: fall back to the part before the dash.
        int dash = wanted.IndexOf('-');
        if (dash > 0)
        {
            string bare = wanted[..dash].Trim();
            return Array.FindIndex(entryNames, s => string.Equals(s, bare, StringComparison.OrdinalIgnoreCase));
        }
        return -1;
    }

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