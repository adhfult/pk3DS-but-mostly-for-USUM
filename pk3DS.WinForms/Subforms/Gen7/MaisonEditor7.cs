using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;

using pk3DS.Core;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class MaisonEditor7 : Form
{
    public MaisonEditor7(byte[][] trd, byte[][] trp, bool royal)
    {
        trFiles = trd;
        pkFiles = trp;
        Array.Resize(ref specieslist, Main.Config.MaxSpeciesID + 1);
        movelist[0] = specieslist[0] = itemlist[0] = "";

        trNames = Main.Config.GetText(royal ? TextName.BattleRoyalNames : TextName.BattleTreeNames); Array.Resize(ref trNames, trFiles.Length);

        InitializeComponent();
        Setup();
        SetupChoiceList();
        RefreshSetList();
        Text = royal ? "Battle Royal Editor" : "Battle Tree Editor";
        WinFormsUtil.ApplyCyberSlateTheme(this, WinFormsUtil.VisualTheme.Grey);
    }

    /// <summary>
    /// Releases the sprites drawn in the set list; a ListBox does not own what an owner-draw
    /// handler paints, so nothing else would free them.
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        foreach (var img in choiceSprites.Values) img?.Dispose();
        choiceSprites.Clear();
        base.OnFormClosed(e);
    }

    private readonly byte[][] trFiles;
    private readonly string[] trNames;
    private readonly byte[][] pkFiles;
    private readonly string[] natures = Main.Config.GetText(TextName.Natures);
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly string[] specieslist = Main.Config.GetText(TextName.SpeciesNames);
    private readonly string[] trClass = Main.Config.GetText(TextName.TrainerClasses);
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private int trEntry = -1;
    private int pkEntry = -1;
    private bool dumping;

    private void Setup()
    {
        for (int i = 0; i < trClass.Length; i++)
            CB_Class.Items.Add($"{trClass[i]} - {i:000}");
        CB_Species.Items.AddRange(specieslist);
        CB_Move1.Items.AddRange(movelist);
        CB_Move2.Items.AddRange(movelist);
        CB_Move3.Items.AddRange(movelist);
        CB_Move4.Items.AddRange(movelist);
        CB_Nature.Items.AddRange(natures);
        CB_Item.Items.AddRange(itemlist);
        for (int i = 0; i < trNames.Length; i++)
            CB_Trainer.Items.Add($"{trNames[i] ?? "UNKNOWN"} - {i:000}");
        for (int i = 0; i < pkFiles.Length; i++)
        {
            var pk = new Maison7.Pokemon(pkFiles[i]);
            string name = pk.Species < specieslist.Length ? specieslist[pk.Species] : "???";
            CB_Pokemon.Items.Add($"{name} - {i:000}");
        }

        CB_Trainer.SelectedIndex = 0;
    }

    private void UpdateMascot()
    {
        int species = Main.Config.USUM ? 791 : 722; // Solgaleo or Rowlet
        WinFormsUtil.SetImage(PB_Mascot, WinFormsUtil.GetSprite(species, 0, 0, 0, Main.Config));
    }

    private void ChangeTrainer(object sender, EventArgs e)
    {
        SetTrainer();
        trEntry = CB_Trainer.SelectedIndex;
        GetTrainer();
        RefreshSetList();
    }

    private void ChangePokemon(object sender, EventArgs e)
    {
        SetPokemon();
        pkEntry = CB_Pokemon.SelectedIndex;
        GetPokemon();
    }

    private void GetTrainer()
    {
        if (trEntry < 0) return;

        // Get
        LB_Choices.Items.Clear();
        var tr = new Maison7.Trainer(trFiles[trEntry]);

        CB_Class.SelectedIndex = tr.Class;
        GB_Trainer.Enabled = tr.Count > 0;

        foreach (ushort Entry in tr.Choices)
            LB_Choices.Items.Add(Entry.ToString());
    }

    /// <summary>Sprites drawn in the set list, cached per set and released when the form closes.</summary>
    private readonly Dictionary<int, Image> choiceSprites = [];

    /// <summary>
    /// Draws each set in the trainer's list as its sprite and species name.
    /// <para>
    /// The list holds set indices, and it used to show them as the bare numbers they are - a column
    /// reading "1, 2, 19, 20, 21" with nothing to say which Pokémon those are. The underlying items
    /// are still the indices, so saving is unchanged; only what is on screen differs.
    /// </para>
    /// </summary>
    private void SetupChoiceList()
    {
        LB_Choices.DrawMode = DrawMode.OwnerDrawFixed;
        LB_Choices.ItemHeight = 34;
        LB_Choices.DrawItem += DrawChoiceItem;
    }

    private void DrawChoiceItem(object sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= LB_Choices.Items.Count) return;

        if (!int.TryParse(LB_Choices.Items[e.Index]?.ToString(), out int setIndex))
            return;

        var bounds = e.Bounds;
        var sprite = GetChoiceSprite(setIndex);
        if (sprite != null)
            e.Graphics.DrawImage(sprite, bounds.X + 2, bounds.Y + 2, 32, 30);

        // The set's own label, which already reads "Species - 000".
        string label = setIndex >= 0 && setIndex < CB_Pokemon.Items.Count
            ? CB_Pokemon.Items[setIndex]?.ToString() ?? $"Set {setIndex}"
            : $"Set {setIndex} (out of range)";

        using var brush = new SolidBrush(e.ForeColor);
        var textRect = new Rectangle(bounds.X + 38, bounds.Y + 2, bounds.Width - 40, bounds.Height - 4);
        using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        e.Graphics.DrawString(label, e.Font, brush, textRect, fmt);

        e.DrawFocusRectangle();
    }

    private Image GetChoiceSprite(int setIndex)
    {
        if (choiceSprites.TryGetValue(setIndex, out var cached)) return cached;
        if (setIndex < 0 || setIndex >= pkFiles.Length) return choiceSprites[setIndex] = null;

        try
        {
            var pk = new Maison7.Pokemon(pkFiles[setIndex]);
            var sprite = WinFormsUtil.GetSprite(pk.Species, pk.Form, 0, pk.Item, Main.Config);
            return choiceSprites[setIndex] = sprite;
        }
        catch
        {
            return choiceSprites[setIndex] = null;
        }
    }

    private void SetTrainer()
    {
        if (trEntry < 0 || !GB_Trainer.Enabled || dumping) return;
        // Gather
        var tr = new Maison7.Trainer
        {
            Class = (ushort)CB_Class.SelectedIndex,
            Count = (ushort)LB_Choices.Items.Count,
        };
        tr.Choices = new ushort[tr.Count];
        for (int i = 0; i < tr.Count; i++)
            tr.Choices[i] = Convert.ToUInt16(LB_Choices.Items[i].ToString());
        Array.Sort(tr.Choices);
        trFiles[trEntry] = tr.Write();
    }

    private void GetPokemon()
    {
        if (pkEntry < 0 || dumping) return;
        var pkm = new Maison7.Pokemon(pkFiles[pkEntry]);

        // Get
        CB_Move1.SelectedIndex = pkm.Moves[0];
        CB_Move2.SelectedIndex = pkm.Moves[1];
        CB_Move3.SelectedIndex = pkm.Moves[2];
        CB_Move4.SelectedIndex = pkm.Moves[3];
        CHK_HP.Checked = pkm.HP;
        CHK_ATK.Checked = pkm.ATK;
        CHK_DEF.Checked = pkm.DEF;
        CHK_Spe.Checked = pkm.SPE;
        CHK_SpA.Checked = pkm.SPA;
        CHK_SpD.Checked = pkm.SPD;
        CB_Nature.SelectedIndex = pkm.Nature;
        CB_Item.SelectedIndex = pkm.Item;
        NUD_Form.Value = pkm.Form;

        CB_Species.SelectedIndex = pkm.Species; // Loaded last in order to refresh the sprite with all info.
        // Last 2 Bytes are unused.
    }

    private void SetPokemon()
    {
        if (pkEntry < 0 || dumping) return;

        // Each File is 16 Bytes.
        var pkm = new Maison7.Pokemon(pkFiles[pkEntry])
        {
            Species = (ushort)CB_Species.SelectedIndex,
            HP = CHK_HP.Checked,
            ATK = CHK_ATK.Checked,
            DEF = CHK_DEF.Checked,
            SPE = CHK_Spe.Checked,
            SPA = CHK_SpA.Checked,
            SPD = CHK_SpD.Checked,
            Nature = (byte)CB_Nature.SelectedIndex,
            Item = (ushort)CB_Item.SelectedIndex,
            Move1 = CB_Move1.SelectedIndex,
            Move2 = CB_Move2.SelectedIndex,
            Move3 = CB_Move3.SelectedIndex,
            Move4 = CB_Move4.SelectedIndex,
            Form = (ushort)NUD_Form.Value,
        };

        byte[] data = pkm.Write();
        pkFiles[pkEntry] = data;
    }

    private void ChangeSpecies(object sender, EventArgs e)
    {
        WinFormsUtil.SetImage(PB_PKM, WinFormsUtil.GetSprite(CB_Species.SelectedIndex, (int)NUD_Form.Value, 0, CB_Item.SelectedIndex, Main.Config));
    }

    private void B_Remove_Click(object sender, EventArgs e)
    {
        if (LB_Choices.SelectedIndex > -1 && GB_Trainer.Enabled)
            LB_Choices.Items.RemoveAt(LB_Choices.SelectedIndex);
    }

    private void B_Set_Click(object sender, EventArgs e)
    {
        if (LB_Choices.SelectedIndex <= -1 || !GB_Trainer.Enabled) return;

        int toAdd = CB_Pokemon.SelectedIndex;
        int count = LB_Choices.Items.Count;
        List<ushort> choices = [];
        for (int i = 0; i < count; i++)
            choices.Add(Convert.ToUInt16(LB_Choices.Items[i].ToString()));

        if (Array.IndexOf(choices.ToArray(), toAdd) > 0) return; // Abort if already in the list
        choices.Add((ushort)toAdd); // Add it to the list.

        // Get new list, and sort it.
        ushort[] choiceList = [.. choices]; Array.Sort(choiceList);

        // Set new list.
        LB_Choices.Items.Clear();
        foreach (ushort t in choiceList)
            LB_Choices.Items.Add(t.ToString());

        // Set current index to the one just added.
        LB_Choices.SelectedIndex = Array.IndexOf(choiceList, toAdd);
    }

    private void B_View_Click(object sender, EventArgs e)
    {
        if (LB_Choices.SelectedIndex > -1 && GB_Trainer.Enabled)
            CB_Pokemon.SelectedIndex = Convert.ToUInt16(LB_Choices.Items[LB_Choices.SelectedIndex].ToString());
    }

    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetTrainer();
        SetPokemon();
    }

    private void DumpTRs_Click(object sender, EventArgs e)
    {
        dumping = true;
        string result = "";
        for (int i = 0; i < CB_Trainer.Items.Count; i++)
        {
            CB_Trainer.SelectedIndex = i;
            int count = LB_Choices.Items.Count;
            if (count > 0)
            {
                result += "======" + Environment.NewLine + i + " - (" + CB_Class.Text + ") " + CB_Trainer.Text + Environment.NewLine + "======" + Environment.NewLine;
                result += "Choices: ";
                for (int c = 0; c < count; c++)
                    result += LB_Choices.Items[c] + ", ";

                result += Environment.NewLine; result += Environment.NewLine;
            }
        }
        var sfd = new SaveFileDialog { FileName = "Maison Trainers.txt", Filter = "Text File|*.txt" };

        SystemSounds.Asterisk.Play();
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            string path = sfd.FileName;
            File.WriteAllText(path, result, Encoding.Unicode);
        }
        dumping = false;
        CB_Trainer.SelectedIndex = 1;
        UpdateMascot();
    }
    
    private void B_DumpPKs_Click(object sender, EventArgs e)
    {
        //File.WriteAllBytes("maiz", pkFiles.SelectMany(t => t).ToArray());
        string[] stats = ["HP", "ATK", "DEF", "Spe", "SpA", "SpD"];
        string result = "";
        for (int i = 0; i < pkFiles.Length; i++)
        {
            var pk = new Maison7.Pokemon(pkFiles[i]);
            if (pk.Species == 0)
                continue;

            result += "======" + Environment.NewLine;
            result += $"{i} - {specieslist[pk.Species]}" + Environment.NewLine;
            result += "======" + Environment.NewLine;
            result += $"Held Item: {itemlist[pk.Item]}" + Environment.NewLine;
            result += $"Nature: {natures[pk.Nature]}" + Environment.NewLine;
            result += $"Move 1: {movelist[pk.Move1]}" + Environment.NewLine;
            result += $"Move 2: {movelist[pk.Move2]}" + Environment.NewLine;
            result += $"Move 3: {movelist[pk.Move3]}" + Environment.NewLine;
            result += $"Move 4: {movelist[pk.Move4]}" + Environment.NewLine;

            var EVstr = string.Join(",", pk.EVs.Select((iv, x) => iv ? stats[x] : string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)));
            result += $"EV'd in: {(pk.EVs.Length > 0 ? EVstr : "None")}" + Environment.NewLine;

            if (pk.Form > 0)
                result += $"Form: {pk.Form}" + Environment.NewLine;

            result += Environment.NewLine;
        }
        var sfd = new SaveFileDialog { FileName = "Maison Pokemon.txt", Filter = "Text File|*.txt" };

        if (sfd.ShowDialog() != DialogResult.OK)
            return;

        File.WriteAllText(sfd.FileName, result, Encoding.Unicode);
    }

    private void RefreshPokemonName(int idx)
    {
        if (idx < 0 || idx >= pkFiles.Length) return;
        var pk = new Maison7.Pokemon(pkFiles[idx]);
        string name = pk.Species < specieslist.Length ? specieslist[pk.Species] : "???";
        CB_Pokemon.Items[idx] = $"{name} - {idx:000}";
    }

    private void B_ImportPKs_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        string[] lines = File.ReadAllLines(ofd.FileName);
        int currentIdx = -1;
        int updated = 0;
        dumping = true;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("======"))
            {
                continue;
            }
            // Parse "123 - SpeciesName" format from header line after ======
            if (line.Contains(" - ") && !line.StartsWith("Held") && !line.StartsWith("Nature") && !line.StartsWith("Move") && !line.StartsWith("EV") && !line.StartsWith("Form"))
            {
                string[] parts = line.Split('-');
                if (int.TryParse(parts[0].Trim(), out int idx) && idx >= 0 && idx < pkFiles.Length)
                {
                    currentIdx = idx;
                    updated++;
                }
                continue;
            }
            if (currentIdx < 0 || currentIdx >= pkFiles.Length) continue;

            var pkm = new Maison7.Pokemon(pkFiles[currentIdx]);
            if (line.StartsWith("Held Item: "))
            {
                string itemName = line.Substring(11).Trim();
                int itemIdx = Array.IndexOf(itemlist, itemName);
                if (itemIdx >= 0) { pkm.Item = (ushort)itemIdx; pkFiles[currentIdx] = pkm.Write(); }
            }
            else if (line.StartsWith("Nature: "))
            {
                string natureName = line.Substring(8).Trim();
                int natIdx = Array.IndexOf(natures, natureName);
                if (natIdx >= 0) { pkm.Nature = (byte)natIdx; pkFiles[currentIdx] = pkm.Write(); }
            }
            else if (line.StartsWith("Move 1: ")) { int mi = Array.IndexOf(movelist, line.Substring(8).Trim()); if (mi >= 0) { pkm.Move1 = mi; pkFiles[currentIdx] = pkm.Write(); } }
            else if (line.StartsWith("Move 2: ")) { int mi = Array.IndexOf(movelist, line.Substring(8).Trim()); if (mi >= 0) { pkm.Move2 = mi; pkFiles[currentIdx] = pkm.Write(); } }
            else if (line.StartsWith("Move 3: ")) { int mi = Array.IndexOf(movelist, line.Substring(8).Trim()); if (mi >= 0) { pkm.Move3 = mi; pkFiles[currentIdx] = pkm.Write(); } }
            else if (line.StartsWith("Move 4: ")) { int mi = Array.IndexOf(movelist, line.Substring(8).Trim()); if (mi >= 0) { pkm.Move4 = mi; pkFiles[currentIdx] = pkm.Write(); } }
        }

        dumping = false;
        if (pkEntry >= 0) GetPokemon();
        WinFormsUtil.Alert($"Imported {updated} Pokémon entries!");
    }

    private Maison7.Pokemon ParseShowdownSet(string text)
    {
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;

        var pkm = new Maison7.Pokemon(new byte[16]);

        // Line 1: "Species @ Item" or "Nickname (Species) @ Item"
        string line1 = lines[0];
        string speciesPart = line1;
        string itemPart = null;

        int atIdx = line1.IndexOf(" @ ");
        if (atIdx >= 0)
        {
            speciesPart = line1.Substring(0, atIdx).Trim();
            itemPart = line1.Substring(atIdx + 3).Trim();
        }

        // Handle "Nickname (Species)" format
        int parenOpen = speciesPart.IndexOf('(');
        int parenClose = speciesPart.IndexOf(')');
        if (parenOpen >= 0 && parenClose > parenOpen)
            speciesPart = speciesPart.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();

        // Handle form suffixes like "Rotom-Wash"
        string formName = null;
        int dashIdx = speciesPart.IndexOf('-');
        if (dashIdx > 0)
        {
            formName = speciesPart.Substring(dashIdx + 1);
            speciesPart = speciesPart.Substring(0, dashIdx);
        }

        int speciesIdx = Array.IndexOf(specieslist, speciesPart);
        if (speciesIdx < 0) return null;
        pkm.Species = (ushort)speciesIdx;

        if (itemPart != null)
        {
            int itemIdx = Array.IndexOf(itemlist, itemPart);
            if (itemIdx >= 0) pkm.Item = (ushort)itemIdx;
        }

        int moveSlot = 0;
        foreach (string line in lines.Skip(1))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("- "))
            {
                string moveName = trimmed.Substring(2).Trim();
                int moveIdx = Array.IndexOf(movelist, moveName);
                if (moveIdx >= 0 && moveSlot < 4)
                    pkm.Moves[moveSlot++] = (ushort)moveIdx;
            }
            else if (trimmed.EndsWith("Nature"))
            {
                string natureName = trimmed.Split(' ')[0];
                int natIdx = Array.IndexOf(natures, natureName);
                if (natIdx >= 0) pkm.Nature = (byte)natIdx;
            }
            else if (trimmed.StartsWith("EVs:"))
            {
                // Parse "EVs: 252 HP / 252 Atk / 4 Spe"
                string[] evParts = trimmed.Substring(4).Split('/');
                foreach (string part in evParts)
                {
                    string p = part.Trim();
                    if (!p.Contains("252")) continue; // Only check 252 EVs
                    if (p.Contains("HP")) pkm.HP = true;
                    else if (p.Contains("Atk") && !p.Contains("SpA")) pkm.ATK = true;
                    else if (p.Contains("Def") && !p.Contains("SpD")) pkm.DEF = true;
                    else if (p.Contains("SpA") || p.Contains("Sp.Atk")) pkm.SPA = true;
                    else if (p.Contains("SpD") || p.Contains("Sp.Def")) pkm.SPD = true;
                    else if (p.Contains("Spe")) pkm.SPE = true;
                }
            }
        }
        return pkm;
    }

    private void B_ShowdownImport_Click(object sender, EventArgs e)
    {
        string clipText = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(clipText))
        {
            WinFormsUtil.Alert("No text found in clipboard.", "Copy a Showdown set to your clipboard first.");
            return;
        }

        var pkm = ParseShowdownSet(clipText);
        if (pkm == null)
        {
            WinFormsUtil.Alert("Could not parse the Showdown set.", "Make sure the species name matches the game's species list.");
            return;
        }

        if (pkEntry < 0) { WinFormsUtil.Alert("Select a Pokémon entry first."); return; }

        pkFiles[pkEntry] = pkm.Write();
        GetPokemon();
        RefreshPokemonName(pkEntry);
        WinFormsUtil.Alert($"Imported {specieslist[pkm.Species]} to entry {pkEntry}!");
    }

    private void B_ShowdownBox_Click(object sender, EventArgs e)
    {
        string clipText = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(clipText))
        {
            WinFormsUtil.Alert("No text found in clipboard.", "Copy up to 30 Showdown sets to your clipboard first (separated by blank lines).");
            return;
        }

        // Split sets by double newline
        string[] sets = System.Text.RegularExpressions.Regex.Split(clipText.Trim(), @"\n\s*\n");
        int startIdx = pkEntry >= 0 ? pkEntry : 0;
        int imported = 0;
        List<ushort> addedChoices = [];

        for (int i = 0; i < Math.Min(sets.Length, 30); i++)
        {
            int targetIdx = startIdx + i;
            if (targetIdx >= pkFiles.Length) break;

            var pkm = ParseShowdownSet(sets[i]);
            if (pkm == null) continue;

            pkFiles[targetIdx] = pkm.Write();
            RefreshPokemonName(targetIdx);
            addedChoices.Add((ushort)targetIdx);
            imported++;
        }

        // Auto-assign imported entries to the current trainer
        if (trEntry >= 0 && addedChoices.Count > 0)
        {
            SetTrainer();
            foreach (ushort idx in addedChoices)
            {
                bool exists = false;
                for (int i = 0; i < LB_Choices.Items.Count; i++)
                    if (Convert.ToUInt16(LB_Choices.Items[i].ToString()) == idx) { exists = true; break; }
                if (!exists) LB_Choices.Items.Add(idx.ToString());
            }
        }

        if (pkEntry >= 0) GetPokemon();
        WinFormsUtil.Alert($"Imported {imported} Showdown sets starting at entry {startIdx}!");
    }

    private void B_AssignList_Click(object sender, EventArgs e)
    {
        if (trEntry < 0) { WinFormsUtil.Alert("Select a trainer first."); return; }

        string input = WinFormsUtil.PromptInput(
            "Assign List",
            "Enter Pokémon species names (comma separated) OR paste Showdown sets.\nThis will REPLACE the trainer's current list.",
            "");

        if (string.IsNullOrWhiteSpace(input)) return;

        List<ushort> matchingEntries = [];

        // Check if it looks like Showdown Sets (starts with a species name followed by @ or moves)
        bool isShowdown = input.Contains("@") || input.Contains("- ");

        if (isShowdown)
        {
            string[] sets = System.Text.RegularExpressions.Regex.Split(input.Trim(), @"\n\s*\n");
            int startIdx = pkEntry >= 0 ? pkEntry : 0;
            int imported = 0;

            for (int i = 0; i < sets.Length; i++)
            {
                int targetIdx = startIdx + i;
                if (targetIdx >= pkFiles.Length) break;

                var pkm = ParseShowdownSet(sets[i]);
                if (pkm == null) continue;

                pkFiles[targetIdx] = pkm.Write();
                RefreshPokemonName(targetIdx);
                matchingEntries.Add((ushort)targetIdx);
                imported++;
            }
            WinFormsUtil.Alert($"Imported and assigned {imported} Showdown sets!");
        }
        else
        {
            string[] requestedSpecies = input.Split(',');
            foreach (string sp in requestedSpecies)
            {
                string trimmed = sp.Trim();
                int spIdx = Array.IndexOf(specieslist, trimmed);
                if (spIdx < 0) continue;

                for (int i = 0; i < pkFiles.Length; i++)
                {
                    var pk = new Maison7.Pokemon(pkFiles[i]);
                    if (pk.Species == spIdx && !matchingEntries.Contains((ushort)i))
                        matchingEntries.Add((ushort)i);
                }
            }
        }

        if (matchingEntries.Count == 0)
        {
            WinFormsUtil.Alert("No matching entries found or imported.");
            return;
        }

        matchingEntries.Sort();
        SetTrainer();
        LB_Choices.Items.Clear();
        foreach (ushort idx in matchingEntries)
            LB_Choices.Items.Add(idx.ToString());

        WinFormsUtil.Alert($"assigned {matchingEntries.Count} Pokémon entries to this trainer!");
    }

    private void RefreshSetList()
    {
        LB_StoredSets.Items.Clear();
        LB_StoredSets.Items.AddRange(ShowdownSetManager.GetSetListStrings().ToArray());
    }

    private void B_ShowdownStorage_Click(object sender, EventArgs e)
    {
        using var storage = new ShowdownSetStorage();
        if (storage.ShowDialog() != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(storage.SelectedSet)) return;
        if (pkEntry < 0) return;
        var newPK = ParseShowdownSet(storage.SelectedSet);
        if (newPK == null) return;
        pkFiles[pkEntry] = newPK.Write();
        GetPokemon();
        RefreshPokemonName(pkEntry);
        RefreshSetList();
    }

    private void LB_StoredSets_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (LB_StoredSets.SelectedIndex < 0 || pkEntry < 0) return;
        string text = ShowdownSetManager.GetSetText(LB_StoredSets.SelectedIndex);
        if (string.IsNullOrWhiteSpace(text)) return;
        var newPK = ParseShowdownSet(text);
        if (newPK == null) return;
        pkFiles[pkEntry] = newPK.Write();
        GetPokemon();
        RefreshPokemonName(pkEntry);
    }

    private void B_ExportTeam_Click(object sender, EventArgs e)
    {
        if (trEntry < 0) return;
        var tr = new Maison7.Trainer(trFiles[trEntry]);
        var sb = new StringBuilder();
        foreach (ushort entryIdx in tr.Choices)
        {
            var pk = new Maison7.Pokemon(pkFiles[entryIdx]);
            sb.AppendLine(ExportShowdown(pk)).AppendLine();
        }
        Clipboard.SetText(sb.ToString().Trim());
        WinFormsUtil.Alert("Team exported to Showdown format!");
    }

    private string ExportShowdown(Maison7.Pokemon pk)
    {
        var sb = new StringBuilder();
        string species = specieslist[pk.Species];
        string item = pk.Item < itemlist.Length ? itemlist[pk.Item] : "";
        sb.Append(species);
        if (!string.IsNullOrEmpty(item)) sb.Append(" @ ").Append(item);
        sb.AppendLine();

        sb.AppendLine($"Nature: {natures[pk.Nature]}");
        string evs = string.Join(" / ", new[] { 
            pk.HP ? "252 HP" : "", 
            pk.ATK ? "252 Atk" : "", 
            pk.DEF ? "252 Def" : "", 
            pk.SPA ? "252 SpA" : "", 
            pk.SPD ? "252 SpD" : "", 
            pk.SPE ? "252 Spe" : "" 
        }.Where(s => !string.IsNullOrEmpty(s)));
        if (!string.IsNullOrEmpty(evs)) sb.AppendLine($"EVs: {evs}");

        if (pk.Moves[0] > 0) sb.AppendLine($"- {movelist[pk.Moves[0]]}");
        if (pk.Moves[1] > 0) sb.AppendLine($"- {movelist[pk.Moves[1]]}");
        if (pk.Moves[2] > 0) sb.AppendLine($"- {movelist[pk.Moves[2]]}");
        if (pk.Moves[3] > 0) sb.AppendLine($"- {movelist[pk.Moves[3]]}");

        return sb.ToString();
    }
}