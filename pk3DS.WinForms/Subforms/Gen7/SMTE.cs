using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;
using System.Text.Json;

using pk3DS.Core;
using pk3DS.Core.Randomizers;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class SMTE : Form
{
    private readonly LearnsetRandomizer learn = new(Main.Config, Main.Config.Learnsets);
    private readonly TrainerData7[] Trainers;
    private string[][] AltForms;
    private static int[] SpecialClasses;
    private static readonly int[] ImportantTrainers = Main.Config.USUM ? Legal.ImportantTrainers_USUM : Legal.ImportantTrainers_SM;
    private static int[] FinalEvo = Legal.FinalEvolutions_7;
    private static readonly int[] Legendary = Main.Config.USUM ? Legal.Legendary_USUM : Legal.Legendary_SM;
    private static readonly int[] Mythical = Main.Config.USUM ? Legal.Mythical_USUM : Legal.Mythical_SM;
    private static Dictionary<int, int[]> MegaDictionary;
    private int index = -1;
    private int currentSlot = -1;
    private PictureBox[] pba;
    private CheckBox[] AIBits;
    private Button B_ShowdownStorage;

    private readonly byte[][] trdata;
    private readonly byte[][] trpoke;
    private readonly string[] abilitylist = Main.Config.GetText(TextName.AbilityNames);
    private readonly string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private readonly string[] specieslist = Main.Config.GetText(TextName.SpeciesNames);
    private readonly string[] types = Main.Config.GetText(TextName.Types);
    private readonly string[] natures = Main.Config.GetText(TextName.Natures);
    private readonly string[] forms = Enumerable.Range(0, 1000).Select(i => i.ToString("000")).ToArray();
    private readonly string[] trName = Main.Config.GetText(TextName.TrainerNames);
    private readonly string[] trClass = Main.Config.GetText(TextName.TrainerClasses);
    private readonly TextData TrainerNames;

    public SMTE(byte[][] trd, byte[][] trp)
    {
        trdata = trd;
        trpoke = trp;
        TrainerNames = new TextData(trName);
        InitializeComponent();

        ApplyUITweaks();

        Trainers = new TrainerData7[trdata.Length];
        Setup();
        
        CB_TrainerID.SelectedIndex = 0;
        CB_Moves.SelectedIndex = 0;
        MegaDictionary = GiftEditor6.GetMegaDictionary(Main.Config);

        if (CHK_RandomClass.Checked)
        {
            SpecialClasses = CHK_IgnoreSpecialClass.Checked
                ? Main.Config.USUM
                    ? Legal.SpecialClasses_USUM
                    : Legal.SpecialClasses_SM
                : [];
        }

        RandSettings.GetFormSettings(this, Tab_Rand.Controls);
        ShowdownSetManager.Load();
        RefreshSetList();
        
        L_MoneyTranslated = new Label { Location = new Point(CB_Money.Right + 5, CB_Money.Top + 3), AutoSize = true, Text = "$ 0" };
        Tab_Trainer.Controls.Add(L_MoneyTranslated);
        CB_Money.SelectedIndexChanged += (s, e) => UpdateMoneyTranslation();
        CB_Trainer_Class.SelectedIndexChanged += (s, e) => UpdateMoneyTranslation();
    }

    private Label L_MoneyTranslated;
    private void UpdateMoneyTranslation()
    {
        if (index < 0 || loading) return;
        int lv = Trainers[index].Pokemon.Count > 0 ? Trainers[index].Pokemon.Max(p => p.Level) : 0;
        int money = CB_Money.SelectedIndex;
        L_MoneyTranslated.Text = $"$ {money * lv * 4}";
    }

    private void ApplyUITweaks()
    {
        this.Width += 250;
        if (TC_trdata != null) TC_trdata.Width += 220;
        if (B_Randomize != null) B_Randomize.Left += 220;
        if (B_Dump != null) B_Dump.Left += 220;

        if (PB_Team1 != null)
        {
            int startX = PB_Team1.Left;
            int spacing = PB_Team1.Width + 12;
            PictureBox[] teamBoxes = { PB_Team1, PB_Team2, PB_Team3, PB_Team4, PB_Team5, PB_Team6 };
            for (int i = 0; i < teamBoxes.Length; i++)
                if (teamBoxes[i] != null) teamBoxes[i].Left = startX + (i * spacing);
        }

        Control trainerParent = Tab_Trainer;
        GB_Difficulty = new GroupBox { Text = "Difficulty Bits", Size = new Size(150, 180), Location = new Point(200, GB_AIBits.Top) };
        B_MaxIVsAll = new Button { Text = "Max IVs All", Size = new Size(130, 23), Location = new Point(10, 20) };
        B_DoublesAll = new Button { Text = "Doubles All", Size = new Size(130, 23), Location = new Point(10, 50) };
        B_PokeChangeAll = new Button { Text = "PokeChange All", Size = new Size(130, 23), Location = new Point(10, 80) };
        Button B_FlagAll = new Button { Text = "Master AI Flag All", Size = new Size(130, 23), Location = new Point(10, 110) };
        B_FlagAll.Click += (s, e) => { foreach (var t in Trainers) t.Flag = true; LoadEntry(); };
        CHK_Flag.Text = "Master AI";

        B_MaxIVsAll.Click += B_MaxIVsAll_Click;
        B_DoublesAll.Click += B_DoublesAll_Click;
        B_PokeChangeAll.Click += B_PokeChangeAll_Click;

        GB_Difficulty.Controls.Add(B_MaxIVsAll);
        GB_Difficulty.Controls.Add(B_DoublesAll);
        GB_Difficulty.Controls.Add(B_PokeChangeAll);
        GB_Difficulty.Controls.Add(B_FlagAll);
        trainerParent.Controls.Add(GB_Difficulty);

        Control teamParent = PB_Team6.Parent ?? this;
        int teamButtonsX = PB_Team6.Right + 20;
        int teamButtonsY = PB_Team1.Top;

        B_ImportTeam = new Button { Text = "Import Team", Size = new Size(95, 23), Location = new Point(teamButtonsX, teamButtonsY) };
        B_ExportTeam = new Button { Text = "Export Team", Size = new Size(95, 23), Location = new Point(teamButtonsX, teamButtonsY + 25) };
        
        B_ImportTeam.Click += B_ImportTeam_Click;
        B_ExportTeam.Click += B_ExportTeam_Click;
        
        teamParent.Controls.Add(B_ImportTeam);
        teamParent.Controls.Add(B_ExportTeam);

        GB_Showdown = new GroupBox { Text = "Showdown Set Storage", Size = new Size(270, 100), Location = new Point(12, 190) };
        CB_SetList = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Size = new Size(250, 21), Location = new Point(10, 20) };
        B_SaveSet = new Button { Text = "Save Set", Size = new Size(80, 23), Location = new Point(10, 45) };
        B_DeleteSet = new Button { Text = "Delete", Size = new Size(80, 23), Location = new Point(95, 45) };
        B_ImportSet = new Button { Text = "Import Set", Size = new Size(80, 23), Location = new Point(180, 45) };
        B_ExportSet = new Button { Text = "Export Set", Size = new Size(80, 23), Location = new Point(10, 72) };

        CB_SetList.SelectedIndexChanged += CB_SetList_SelectedIndexChanged;
        B_SaveSet.Click += B_SaveSet_Click;
        B_DeleteSet.Click += B_DeleteSet_Click;
        B_ImportSet.Click += B_ImportSet_Click;
        B_ExportSet.Click += B_ExportSet_Click;

        GB_Showdown.Controls.Add(CB_SetList);
        GB_Showdown.Controls.Add(B_SaveSet);
        GB_Showdown.Controls.Add(B_DeleteSet);
        GB_Showdown.Controls.Add(B_ImportSet);
        GB_Showdown.Controls.Add(B_ExportSet);
        Tab_Moves.Controls.Add(GB_Showdown);

        if (GB_AIBits != null && TC_trdata != null) 
        {
            GB_AIBits.Left = TC_trdata.Width - GB_AIBits.Width - 15;
            GB_Difficulty.Left = GB_AIBits.Left - GB_Difficulty.Width - 10; // Dynamic positioning between items and AI bits
            if (CB_Mode != null) CB_Mode.Left = GB_AIBits.Left;
            if (L_Mode != null) L_Mode.Left = CB_Mode.Left - L_Mode.Width - 5;
            foreach (var chk in GB_AIBits.Controls.OfType<CheckBox>()) chk.Width = 120;
        }

        // Layout tweak for Tab_Moves: sideways buttons and move everything up
        if (Tab_Moves != null)
        {
            B_Clear.Size = new Size(75, 40);
            B_CurrentAttack.Size = new Size(88, 40);
            B_HighAttack.Size = new Size(88, 40);

            B_Clear.Location = new Point(5, 5);
            B_CurrentAttack.Location = new Point(85, 5);
            B_HighAttack.Location = new Point(178, 5);

            if (GB_Moves != null)
            {
                GB_Moves.Location = new Point(5, 50);
                GB_Moves.Width = Tab_Moves.Width - 10;
            }
            if (GB_Showdown != null)
            {
                GB_Showdown.Top = 170;
            }
            B_ShowdownStorage = new Button { Text = "Showdown Set Storage", Size = new Size(270, 30), Location = new Point(10, 275) };
            B_ShowdownStorage.Click += B_ShowdownStorage_Click;
            Tab_Moves.Controls.Add(B_ShowdownStorage);
        }

        if (CB_HPType != null)
        {
            CB_HPType.Top = 230; CB_HPType.Left = 110;
            if (CB_HPType.Parent != null)
                foreach (Control l in CB_HPType.Parent.Controls.OfType<Label>().Where(l => l.Text.Contains("Hidden Power")))
                { l.Top = CB_HPType.Top + 4; l.Left = CB_HPType.Left - l.Width - 5; }
        }
        if (B_EnableMega != null) B_EnableMega.Visible = false;
        if (B_EnableZMove != null) B_EnableZMove.Visible = false;
    }

    private int GetSlot(object sender) => Array.IndexOf(pba, sender as PictureBox);

    private void ClickSlot(object sender, MouseEventArgs e)
    {
        int slot = GetSlot(sender); if (slot == -1) return;
        if (e.Button == MouseButtons.Left)
        {
            if (currentSlot == slot)
            {
                if (slot < Trainers[index].NumPokemon)
                {
                    var dr = MessageBox.Show("What would you like to do with this slot?\n\n[Yes] Overwrite with current changes\n[No] Delete Pokémon from team\n[Cancel] Do nothing", "Slot Actions", MessageBoxButtons.YesNoCancel);
                    if (dr == DialogResult.Yes)
                    {
                        Trainers[index].Pokemon[slot] = PrepareTP7();
                        GetQuickFiller(pba[slot], Trainers[index].Pokemon[slot]);
                    }
                    else if (dr == DialogResult.No)
                    {
                        Trainers[index].Pokemon.RemoveAt(slot); Trainers[index].NumPokemon = (int)--NUD_NumPoke.Value;
                        PopulateTeam(Trainers[index]); GetSlotColor(slot, null); currentSlot = -1;
                    }
                    return;
                }
            }

            if (currentSlot != -1 && currentSlot < Trainers[index].NumPokemon)
            {
                var pk_current = PrepareTP7();
                if (!pk_current.Write().SequenceEqual(Trainers[index].Pokemon[currentSlot].Write()))
                {
                    var dr = MessageBox.Show("Save changes to current slot?", "Unsaved Changes", MessageBoxButtons.YesNo);
                    if (dr == DialogResult.Yes)
                    {
                        Trainers[index].Pokemon[currentSlot] = pk_current;
                        GetQuickFiller(pba[currentSlot], pk_current);
                    }
                }
            }
            if (slot < Trainers[index].NumPokemon) { var pk = Trainers[index].Pokemon[slot]; try { PopulateFieldsTP7(pk); } catch { } GetSlotColor(slot, Properties.Resources.slotView); currentSlot = slot; }
            else if (slot == Trainers[index].NumPokemon && slot < 6)
            {
                if (CB_Species.SelectedIndex == 0) { WinFormsUtil.Alert("Can't set empty slot."); return; }
                if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Add this Pokémon to the team?") != DialogResult.Yes) return;
                var pk = PrepareTP7(); Trainers[index].Pokemon.Add(pk); Trainers[index].NumPokemon = (int)++NUD_NumPoke.Value;
                GetQuickFiller(pba[slot], pk); GetSlotColor(slot, Properties.Resources.slotView); currentSlot = slot;
            }
        }
    }

    private void PopulateTeam(TrainerData7 tr) { for (int i = 0; i < tr.NumPokemon; i++) GetQuickFiller(pba[i], tr.Pokemon[i]); for (int i = tr.NumPokemon; i < 6; i++) pba[i].Image = null; }
    private void GetSlotColor(int slot, Image color) { foreach (var t in pba) t.BackgroundImage = null; if (slot >= 0 && slot < pba.Length) pba[slot].BackgroundImage = color; }

    private static void GetQuickFiller(PictureBox pb, TrainerPoke7 pk)
    {
        Bitmap rawImg = WinFormsUtil.GetSprite(pk.Species, pk.Form, pk.Gender, pk.Item, Main.Config, pk.Shiny);
        pb.Image = WinFormsUtil.ScaleImage(rawImg, 2);
    }

    private void RefreshFormAbility(object sender, EventArgs e) { if (index < 0) return; pkm.Form = CB_Forme.SelectedIndex; RefreshPKMSlotAbility(); }
    private void RefreshSpeciesAbility(object sender, EventArgs e) { if (index < 0) return; pkm.Species = (ushort)CB_Species.SelectedIndex; FormUtil.SetForms(CB_Species.SelectedIndex, CB_Forme, AltForms); RefreshPKMSlotAbility(); }

    private void RefreshPKMSlotAbility()
    {
        int pr = CB_Ability.SelectedIndex; int sp = CB_Species.SelectedIndex; int fm = CB_Forme.SelectedIndex;
        sp = Main.SpeciesStat[sp].FormeIndex(sp, fm); CB_Ability.Items.Clear(); CB_Ability.Items.Add("Any (1 or 2)");
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[sp].Abilities[0]] + " (1)");
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[sp].Abilities[1]] + " (2)");
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[sp].Abilities[2]] + " (H)");
        CB_Ability.SelectedIndex = pr;
    }

    private static string GetEntryTitle(string str, int i) => $"{str} - {i:000}";

    private void Setup()
    {
        AltForms = forms.Select(_ => Enumerable.Range(0, 100).Select(i => i.ToString()).ToArray()).ToArray();
        CB_TrainerID.Items.Clear(); for (int i = 0; i < trdata.Length; i++) CB_TrainerID.Items.Add(GetEntryTitle(trName[i] ?? "UNKNOWN", i));
        CB_Trainer_Class.Items.Clear(); for (int i = 0; i < trClass.Length; i++) CB_Trainer_Class.Items.Add(GetEntryTitle(trClass[i], i));
        Trainers[0] = new TrainerData7(); for (int i = 1; i < trdata.Length; i++) Trainers[i] = new TrainerData7(trdata[i], trpoke[i]) { Name = trName[i], ID = i };
        specieslist[0] = "---"; abilitylist[0] = itemlist[0] = movelist[0] = "(None)";
        pba = [PB_Team1, PB_Team2, PB_Team3, PB_Team4, PB_Team5, PB_Team6]; foreach (var pb in pba) pb.MouseClick += ClickSlot;
        AIBits = [CHK_AI0, CHK_AI1, CHK_AI2, CHK_AI3, CHK_AI4, CHK_AI5, CHK_AI6, CHK_AI7];
        CB_Species.Items.Clear(); CB_Species.Items.AddRange(specieslist);
        CB_Move1.Items.Clear(); CB_Move2.Items.Clear(); CB_Move3.Items.Clear(); CB_Move4.Items.Clear();
        foreach (string s in movelist) { CB_Move1.Items.Add(s); CB_Move2.Items.Add(s); CB_Move3.Items.Add(s); CB_Move4.Items.Add(s); }
        CB_HPType.DataSource = types.Skip(1).Take(16).ToArray();
        CB_Nature.Items.Clear(); CB_Nature.Items.AddRange(natures.Take(25).ToArray());
        CB_Item.Items.Clear(); CB_Item.Items.AddRange(itemlist);
        CB_Gender.Items.Clear(); CB_Gender.Items.Add("- / Genderless/Random"); CB_Gender.Items.Add("♂ / Male"); CB_Gender.Items.Add("♀ / Female");
        CB_Forme.Items.Add(""); CB_Species.SelectedIndex = 0;
        CB_Item_1.Items.Clear(); CB_Item_2.Items.Clear(); CB_Item_3.Items.Clear(); CB_Item_4.Items.Clear();
        foreach (string s in itemlist) { CB_Item_1.Items.Add(s); CB_Item_2.Items.Add(s); CB_Item_3.Items.Add(s); CB_Item_4.Items.Add(s); }
        CB_Money.Items.Clear(); for (int i = 0; i < 256; i++) CB_Money.Items.Add(i.ToString());
        
        // Autocomplete
        CB_Species.AutoCompleteMode = CB_TrainerID.AutoCompleteMode = CB_Trainer_Class.AutoCompleteMode = 
        CB_Move1.AutoCompleteMode = CB_Move2.AutoCompleteMode = CB_Move3.AutoCompleteMode = CB_Move4.AutoCompleteMode = 
        CB_Nature.AutoCompleteMode = CB_Item.AutoCompleteMode = CB_Item_1.AutoCompleteMode = CB_Item_2.AutoCompleteMode = 
        CB_Item_3.AutoCompleteMode = CB_Item_4.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        
        CB_Species.AutoCompleteSource = CB_TrainerID.AutoCompleteSource = CB_Trainer_Class.AutoCompleteSource = 
        CB_Move1.AutoCompleteSource = CB_Move2.AutoCompleteSource = CB_Move3.AutoCompleteSource = CB_Move4.AutoCompleteSource = 
        CB_Nature.AutoCompleteSource = CB_Item.AutoCompleteSource = CB_Item_1.AutoCompleteSource = CB_Item_2.AutoCompleteSource = 
        CB_Item_3.AutoCompleteSource = CB_Item_4.AutoCompleteSource = AutoCompleteSource.ListItems;

        CB_TrainerID.SelectedIndex = 0; index = 0; pkm = new TrainerPoke7(); PopulateFieldsTP7(pkm);
    }

    private void ChangeTrainerIndex(object sender, EventArgs e)
    {
        if (currentSlot != -1 && index >= 0 && currentSlot < Trainers[index].NumPokemon)
        {
            var pk_current = PrepareTP7();
            if (!pk_current.Write().SequenceEqual(Trainers[index].Pokemon[currentSlot].Write()))
            {
                var dr = MessageBox.Show("Save changes to current slot before switching trainers?", "Unsaved Changes", MessageBoxButtons.YesNo);
                if (dr == DialogResult.Yes)
                {
                    Trainers[index].Pokemon[currentSlot] = pk_current;
                    GetQuickFiller(pba[currentSlot], pk_current);
                }
            }
        }
        currentSlot = -1; SaveEntry(); LoadEntry(); if (TC_trdata.SelectedIndex == TC_trdata.TabCount - 1) TC_trdata.SelectedIndex = 0;
    }

    private void SaveEntry() { if (index < 0) return; var tr = Trainers[index]; PrepareTR7(tr); SaveData(tr, index); TrainerNames[index] = TB_TrainerName.Text; }
    private void SaveData(TrainerData7 tr, int i) { tr.Write(out byte[] trd, out byte[] trp); trdata[i] = trd; trpoke[i] = trp; }

    private void LoadEntry() { index = CB_TrainerID.SelectedIndex; var tr = Trainers[index]; loading = true; TB_TrainerName.Text = TrainerNames[index]; PopulateFieldsTD7(tr); loading = false; }

    private bool loading;
    private TrainerPoke7 pkm;

    private void PopulateFieldsTP7(TrainerPoke7 pk)
    {
        pkm = pk.Clone(); CB_Species.SelectedIndex = pkm.Species; CB_Forme.SelectedIndex = pkm.Form; CB_Ability.SelectedIndex = pkm.Ability;
        CB_Item.SelectedIndex = pkm.Item; CHK_Shiny.Checked = pkm.Shiny; CB_Gender.SelectedIndex = pkm.Gender;
        CB_Move1.SelectedIndex = pkm.Move1; CB_Move2.SelectedIndex = pkm.Move2; CB_Move3.SelectedIndex = pkm.Move3; CB_Move4.SelectedIndex = pkm.Move4;
        updatingStats = true; CB_Nature.SelectedIndex = pkm.Nature; NUD_Level.Value = Math.Min(NUD_Level.Maximum, pkm.Level);
        TB_HPIV.Text = pkm.IV_HP.ToString(); TB_ATKIV.Text = pkm.IV_ATK.ToString(); TB_DEFIV.Text = pkm.IV_DEF.ToString();
        TB_SPAIV.Text = pkm.IV_SPA.ToString(); TB_SPEIV.Text = pkm.IV_SPE.ToString(); TB_SPDIV.Text = pkm.IV_SPD.ToString();
        TB_HPEV.Text = pkm.EV_HP.ToString(); TB_ATKEV.Text = pkm.EV_ATK.ToString(); TB_DEFEV.Text = pkm.EV_DEF.ToString();
        TB_SPAEV.Text = pkm.EV_SPA.ToString(); TB_SPEEV.Text = pkm.EV_SPE.ToString(); TB_SPDEV.Text = pkm.EV_SPD.ToString();
        if (TB_Happiness != null) TB_Happiness.Text = pkm.Friendship.ToString();
        var ts = new TrackBar[] { TB_HPEV_Slider, TB_ATKEV_Slider, TB_DEFEV_Slider, TB_SPAEV_Slider, TB_SPDEV_Slider, TB_SPEEV_Slider };
        var tbs = new MaskedTextBox[] { TB_HPEV, TB_ATKEV, TB_DEFEV, TB_SPAEV, TB_SPDEV, TB_SPEEV };
        for(int i = 0; i < 6; i++) if (ts[i] != null) { int val = WinFormsUtil.ToInt32(tbs[i]); ts[i].Value = val > 252 ? 252 : val; }
        updatingStats = false; UpdateStats(null, null);
    }

    private TrainerPoke7 PrepareTP7()
    {
        var pk = pkm.Clone(); pk.Species = CB_Species.SelectedIndex; pk.Form = CB_Forme.SelectedIndex; pk.Level = (byte)NUD_Level.Value;
        pk.Ability = CB_Ability.SelectedIndex; pk.Item = CB_Item.SelectedIndex; pk.Shiny = CHK_Shiny.Checked; pk.Nature = CB_Nature.SelectedIndex; pk.Gender = CB_Gender.SelectedIndex;
        pk.Move1 = CB_Move1.SelectedIndex; pk.Move2 = CB_Move2.SelectedIndex; pk.Move3 = CB_Move3.SelectedIndex; pk.Move4 = CB_Move4.SelectedIndex;
        pk.IV_HP = WinFormsUtil.ToInt32(TB_HPIV); pk.IV_ATK = WinFormsUtil.ToInt32(TB_ATKIV); pk.IV_DEF = WinFormsUtil.ToInt32(TB_DEFIV);
        pk.IV_SPA = WinFormsUtil.ToInt32(TB_SPAIV); pk.IV_SPE = WinFormsUtil.ToInt32(TB_SPEIV); pk.IV_SPD = WinFormsUtil.ToInt32(TB_SPDIV);
        pk.EV_HP = WinFormsUtil.ToInt32(TB_HPEV); pk.EV_ATK = WinFormsUtil.ToInt32(TB_ATKEV); pk.EV_DEF = WinFormsUtil.ToInt32(TB_DEFEV);
        pk.EV_SPA = WinFormsUtil.ToInt32(TB_SPAEV); pk.EV_SPE = WinFormsUtil.ToInt32(TB_SPEEV); pk.EV_SPD = WinFormsUtil.ToInt32(TB_SPDEV);
        if (TB_Happiness != null) pk.Friendship = WinFormsUtil.ToInt32(TB_Happiness); return pk;
    }

    private void PopulateFieldsTD7(TrainerData7 tr)
    {
        CB_Trainer_Class.SelectedIndex = tr.TrainerClass; NUD_NumPoke.Value = tr.NumPokemon;
        CB_Item_1.SelectedIndex = tr.Item1; CB_Item_2.SelectedIndex = tr.Item2; CB_Item_3.SelectedIndex = tr.Item3; CB_Item_4.SelectedIndex = tr.Item4;
        CB_Money.SelectedIndex = tr.Money; CB_Mode.SelectedIndex = (int)tr.Mode; LoadAIBits((uint)tr.AI); CHK_Flag.Checked = tr.Flag; PopulateTeam(tr);
    }

    private void PrepareTR7(TrainerData7 tr)
    {
        tr.TrainerClass = (byte)CB_Trainer_Class.SelectedIndex; tr.NumPokemon = (byte)NUD_NumPoke.Value;
        tr.Item1 = CB_Item_1.SelectedIndex; tr.Item2 = CB_Item_2.SelectedIndex; tr.Item3 = CB_Item_3.SelectedIndex; tr.Item4 = CB_Item_4.SelectedIndex;
        tr.Money = CB_Money.SelectedIndex; tr.Mode = (BattleMode)CB_Mode.SelectedIndex; tr.AI = (int)SaveAIBits(); tr.Flag = CHK_Flag.Checked;
    }

    private void LoadAIBits(uint val) { for (int i = 0; i < AIBits.Length; i++) AIBits[i].Checked = ((val >> i) & 1) == 1; }
    private uint SaveAIBits() { uint val = 0; for (int i = 0; i < AIBits.Length; i++) val |= AIBits[i].Checked ? 1u << i : 0; return val; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (currentSlot != -1 && index >= 0 && currentSlot < Trainers[index].NumPokemon) Trainers[index].Pokemon[currentSlot] = PrepareTP7();
        SaveEntry(); if (TrainerNames.Modified) Main.Config.SetText(TextName.TrainerNames, TrainerNames.Lines);
        base.OnFormClosing(e); RandSettings.SetFormSettings(this, Tab_Rand.Controls);
    }

    private void DumpTxt(object sender, EventArgs e)
    {
        var sfd = new SaveFileDialog { FileName = "Trainers.txt" }; if (sfd.ShowDialog() != DialogResult.OK) return;
        var sb = new StringBuilder(); foreach (var tr in Trainers) sb.Append(GetTrainerString(tr)); File.WriteAllText(sfd.FileName, sb.ToString());
    }

    private string GetTrainerString(TrainerData7 tr)
    {
        var sb = new StringBuilder(); sb.AppendLine("======"); sb.Append(tr.ID).Append(" - ").Append(trClass[tr.TrainerClass]).Append(' ').AppendLine(tr.Name); sb.AppendLine("======");
        sb.Append("Pokemon: ").Append(tr.NumPokemon).AppendLine();
        for (int i = 0; i < tr.NumPokemon; i++)
        {
            var p = tr.Pokemon[i]; if (p.Shiny) sb.Append("Shiny "); sb.Append(specieslist[p.Species]); sb.Append(" (Lv. ").Append(p.Level).Append(") ");
            if (p.Item > 0) sb.Append('@').Append(itemlist[p.Item]);
            if (p.Nature != 0) sb.Append(" (Nature: ").Append(natures[p.Nature]).Append(')');
            sb.Append(" (Moves: ").AppendJoin("/", p.Moves.Select(m => m == 0 ? "(None)" : movelist[m])).Append(')');
            sb.Append(" IVs: ").AppendJoin("/", p.IVs); sb.Append(" EVs: ").AppendJoin("/", p.EVs); sb.AppendLine();
        }
        return sb.ToString();
    }

    private void UpdateNumPokemon(object sender, EventArgs e) { if (index < 0) return; Trainers[index].NumPokemon = (int)NUD_NumPoke.Value; }
    private void UpdateTrainerName(object sender, EventArgs e) { if (loading) return; string str = TB_TrainerName.Text; CB_TrainerID.Items[index] = GetEntryTitle(str, index); }
    private static bool updatingStats;

    private void UpdateStats(object sender, EventArgs e)
    {
        if (updatingStats) return; var tb_iv = new[] { TB_HPIV, TB_ATKIV, TB_DEFIV, TB_SPEIV, TB_SPAIV, TB_SPDIV }; var tb_ev = new[] { TB_HPEV, TB_ATKEV, TB_DEFEV, TB_SPEEV, TB_SPAEV, TB_SPDEV };
        for (int j = 0; j < 6; j++) { updatingStats = true; if (WinFormsUtil.ToInt32(tb_iv[j]) > 31) tb_iv[j].Text = "31"; if (WinFormsUtil.ToInt32(tb_ev[j]) > 255) tb_ev[j].Text = "255"; updatingStats = false; }
        int sp = CB_Species.SelectedIndex; sp = Main.SpeciesStat[sp].FormeIndex(sp, CB_Forme.SelectedIndex); var p = Main.SpeciesStat[sp]; int lv = (int)NUD_Level.Value; int na = CB_Nature.SelectedIndex;
        ushort[] st = new ushort[6]; st[0] = (ushort)(p.HP == 1 ? 1 : ((Util.ToInt32(TB_HPIV.Text) + (2 * p.HP) + (Util.ToInt32(TB_HPEV.Text) / 4) + 100) * lv / 100) + 10);
        st[1] = (ushort)(((Util.ToInt32(TB_ATKIV.Text) + (2 * p.ATK) + (Util.ToInt32(TB_ATKEV.Text) / 4)) * lv / 100) + 5);
        st[2] = (ushort)(((Util.ToInt32(TB_DEFIV.Text) + (2 * p.DEF) + (Util.ToInt32(TB_DEFEV.Text) / 4)) * lv / 100) + 5);
        st[4] = (ushort)(((Util.ToInt32(TB_SPAIV.Text) + (2 * p.SPA) + (Util.ToInt32(TB_SPAEV.Text) / 4)) * lv / 100) + 5);
        st[5] = (ushort)(((Util.ToInt32(TB_SPDIV.Text) + (2 * p.SPD) + (Util.ToInt32(TB_SPDEV.Text) / 4)) * lv / 100) + 5);
        st[3] = (ushort)(((Util.ToInt32(TB_SPEIV.Text) + (2 * p.SPE) + (Util.ToInt32(TB_SPEEV.Text) / 4)) * lv / 100) + 5);
        int incr = (na / 5) + 1; int decr = (na % 5) + 1; if (incr != decr) { st[incr] = (ushort)(st[incr] * 1.1); st[decr] = (ushort)(st[decr] * 0.9); }
        Stat_HP.Text = st[0].ToString(); Stat_ATK.Text = st[1].ToString(); Stat_DEF.Text = st[2].ToString(); Stat_SPA.Text = st[4].ToString(); Stat_SPD.Text = st[5].ToString(); Stat_SPE.Text = st[3].ToString();
        TB_IVTotal.Text = tb_iv.Sum(WinFormsUtil.ToInt32).ToString(); TB_EVTotal.Text = tb_ev.Sum(WinFormsUtil.ToInt32).ToString();
        { incr--; decr--; var las = new[] { Label_ATK, Label_DEF, Label_SPE, Label_SPA, Label_SPD }; foreach (var l in las) l.ResetForeColor(); if (incr != decr) { las[incr].ForeColor = Color.Red; las[decr].ForeColor = Color.Blue; } }
        var ivs = tb_iv.Select(tb => WinFormsUtil.ToInt32(tb) & 1).ToArray(); updatingStats = true; CB_HPType.SelectedIndex = 15 * (ivs[0] + (2 * ivs[1]) + (4 * ivs[2]) + (8 * ivs[3]) + (16 * ivs[4]) + (32 * ivs[5])) / 63; updatingStats = false;
    }

    private void UpdateHPType(object sender, EventArgs e)
    {
        if (updatingStats) return; var tb_iv = new[] { TB_HPIV, TB_ATKIV, TB_DEFIV, TB_SPAIV, TB_SPDIV, TB_SPEIV }; int[] newIVs = SetHPIVs(CB_HPType.SelectedIndex, tb_iv.Select(WinFormsUtil.ToInt32).ToArray());
        updatingStats = true; TB_HPIV.Text = newIVs[0].ToString(); TB_ATKIV.Text = newIVs[1].ToString(); TB_DEFIV.Text = newIVs[2].ToString(); TB_SPAIV.Text = newIVs[3].ToString(); TB_SPDIV.Text = newIVs[4].ToString(); TB_SPEIV.Text = newIVs[5].ToString(); updatingStats = false;
    }

    public static int[] SetHPIVs(int t, int[] ivs) { for (int i = 0; i < 6; i++) ivs[i] = (ivs[i] & 0x1E) + hpivs[t, i]; return ivs; }
    private static readonly int[,] hpivs = { { 1, 1, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 1 }, { 1, 1, 0, 0, 0, 1 }, { 1, 1, 1, 0, 0, 1 }, { 1, 1, 0, 1, 0, 0 }, { 1, 0, 0, 1, 0, 1 }, { 1, 0, 1, 1, 0, 1 }, { 1, 1, 1, 1, 0, 1 }, { 1, 0, 1, 0, 1, 0 }, { 1, 0, 0, 0, 1, 1 }, { 1, 0, 1, 0, 1, 1 }, { 1, 1, 1, 0, 1, 1 }, { 1, 0, 1, 1, 1, 0 }, { 1, 0, 0, 1, 1, 1 }, { 1, 0, 1, 1, 1, 1 }, { 1, 1, 1, 1, 1, 1 } };

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize all?") != DialogResult.Yes) return;
        CB_TrainerID.SelectedIndex = 0; var rnd = new SpeciesRandomizer(Main.Config) { G1 = CHK_G1.Checked, G2 = CHK_G2.Checked, G3 = CHK_G3.Checked, G4 = CHK_G4.Checked, G5 = CHK_G5.Checked, G6 = CHK_G6.Checked, G7 = CHK_G7.Checked, E = CHK_E.Checked, L = CHK_L.Checked, rBST = CHK_BST.Checked }; rnd.Initialize();
        var move = new MoveRandomizer(Main.Config) { rSTABCount = (int)NUD_STAB.Value, rDMGCount = (int)NUD_Damage.Value };
        var items = Randomizer.GetRandomItemList();
        for (int i = 0; i < Trainers.Length; i++)
        {
            var tr = Trainers[i]; if (tr.Pokemon.Count == 0) continue;
            foreach (var pk in tr.Pokemon) { if (CHK_RandomPKM.Checked) pk.Species = rnd.GetRandomSpecies(pk.Species); if (CHK_RandomShiny.Checked) pk.Shiny = (int)Util.Random32() % 100 < NUD_Shiny.Value; pk.Moves = move.GetRandomMoveset(pk.Species); }
        }
        LoadEntry(); WinFormsUtil.Alert("Done!");
    }

    private void B_ImportSet_Click(object sender, EventArgs e) { if (currentSlot < 0) return; var pk = Trainers[index].Pokemon[currentSlot]; ParseShowdownSet(Clipboard.GetText(), pk); PopulateFieldsTP7(pk); GetQuickFiller(pba[currentSlot], pk); }
    private void B_ImportTeam_Click(object sender, EventArgs e)
    {
        string[] sets = Clipboard.GetText().Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries); var tr = Trainers[index]; tr.Pokemon.Clear();
        int count = Math.Min(6, sets.Length); for (int i = 0; i < count; i++) { var pk = new TrainerPoke7(); ParseShowdownSet(sets[i], pk); tr.Pokemon.Add(pk); }
        tr.NumPokemon = count; NUD_NumPoke.Value = count; PopulateTeam(tr); if (count > 0) PopulateFieldsTP7(tr.Pokemon[0]);
    }

    private void ParseShowdownSet(string set, TrainerPoke7 pk)
    {
        string[] lines = set.Split('\n'); int hpiv=31, atkiv=31, defiv=31, spaiv=31, spdiv=31, speiv=31, hpev=0, atkev=0, defev=0, spaev=0, spdev=0, speev=0;
        int moveIdx = 0; int[] moves = new int[4];
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].Trim(); if (i == 0) { string[] p = l.Split('@'); string s = p[0].Trim(); if (s.Contains("(")) s = s.Substring(0, s.IndexOf("(")).Trim(); pk.Species = (ushort)Math.Max(0, Array.FindIndex(specieslist, x => x.Equals(s, StringComparison.OrdinalIgnoreCase))); if (p.Length > 1) pk.Item = Math.Max(0, Array.FindIndex(itemlist, x => x.Equals(p[1].Trim(), StringComparison.OrdinalIgnoreCase))); continue; }
            if (l.StartsWith("Ability:")) pk.Ability = Math.Max(0, Array.FindIndex(abilitylist, x => x.Equals(l.Replace("Ability:", "").Trim(), StringComparison.OrdinalIgnoreCase)));
            else if (l.StartsWith("Level:")) { if (int.TryParse(l.Replace("Level:", "").Trim(), out int lv)) pk.Level = lv; }
            else if (l.StartsWith("EVs:")) ParseStats(l.Replace("EVs:", ""), ref hpev, ref atkev, ref defev, ref spaev, ref spdev, ref speev);
            else if (l.StartsWith("IVs:")) ParseStats(l.Replace("IVs:", ""), ref hpiv, ref atkiv, ref defiv, ref spaiv, ref spdiv, ref speiv);
            else if (l.EndsWith("Nature")) pk.Nature = Math.Max(0, Array.FindIndex(natures, x => x.Equals(l.Replace("Nature", "").Trim(), StringComparison.OrdinalIgnoreCase)));
            else if (l.StartsWith("-") && moveIdx < 4) moves[moveIdx++] = Math.Max(0, Array.FindIndex(movelist, x => x.Equals(l.Substring(1).Trim(), StringComparison.OrdinalIgnoreCase)));
        }
        pk.IVs = [hpiv, atkiv, defiv, spaiv, spdiv, speiv]; pk.EVs = [hpev, atkev, defev, spaev, spdev, speev]; pk.Moves = moves;
    }

    private void ParseStats(string s, ref int hp, ref int atk, ref int def, ref int spa, ref int spd, ref int spe)
    {
        foreach (string p in s.Split('/'))
        {
            string[] sp = p.Trim().Split(' '); if (sp.Length < 2 || !int.TryParse(sp[0], out int v)) continue;
            string t = sp[1].ToLower(); if (t == "hp") hp = v; else if (t == "atk") atk = v; else if (t == "def") def = v; else if (t == "spa") spa = v; else if (t == "spd") spd = v; else if (t == "spe") spe = v;
        }
    }

    private void RefreshSetList() { CB_SetList.Items.Clear(); foreach (var s in ShowdownSetManager.Sets) CB_SetList.Items.Add(s.Nickname); }
    private void B_SaveSet_Click(object sender, EventArgs e) { if (currentSlot < 0) return; ShowdownSetManager.AddSet(GetExportString(Trainers[index].Pokemon[currentSlot]), "Set " + (CB_SetList.Items.Count + 1)); RefreshSetList(); }
    private void B_DeleteSet_Click(object sender, EventArgs e) { if (CB_SetList.SelectedIndex >= 0) { ShowdownSetManager.RemoveSet(CB_SetList.SelectedIndex); RefreshSetList(); } }
    private void CB_SetList_SelectedIndexChanged(object sender, EventArgs e) { if (CB_SetList.SelectedIndex < 0 || currentSlot < 0) return; ParseShowdownSet(ShowdownSetManager.Sets[CB_SetList.SelectedIndex].Content, Trainers[index].Pokemon[currentSlot]); PopulateFieldsTP7(Trainers[index].Pokemon[currentSlot]); }
    private string GetExportString(TrainerPoke7 pk) { StringBuilder sb = new StringBuilder(); sb.AppendLine($"{specieslist[pk.Species]} @ {itemlist[pk.Item]}"); sb.AppendLine($"Ability: {abilitylist[pk.Ability]}"); sb.AppendLine($"Level: {pk.Level}"); sb.AppendLine($"{natures[pk.Nature]} Nature"); foreach (int m in pk.Moves) if (m > 0) sb.AppendLine($"- {movelist[m]}"); return sb.ToString(); }
    private void B_ExportTeam_Click(object sender, EventArgs e) { StringBuilder sb = new StringBuilder(); foreach (var p in Trainers[index].Pokemon) sb.AppendLine(GetExportString(p)); Clipboard.SetText(sb.ToString()); }
    private void B_ExportSet_Click(object sender, EventArgs e) { if (currentSlot >= 0) Clipboard.SetText(GetExportString(Trainers[index].Pokemon[currentSlot])); }

    private void B_MaxIVsAll_Click(object sender, EventArgs e) { foreach (var t in Trainers) foreach (var p in t.Pokemon) p.IV_HP = p.IV_ATK = p.IV_DEF = p.IV_SPA = p.IV_SPD = p.IV_SPE = 31; LoadEntry(); }
    private void B_DoublesAll_Click(object sender, EventArgs e) { foreach (var t in Trainers) t.Mode = BattleMode.Doubles; LoadEntry(); }
    private void B_PokeChangeAll_Click(object sender, EventArgs e) { foreach (var t in Trainers) t.AI |= (1 << 6); LoadEntry(); }
    private void B_Master_Click(object sender, EventArgs e) { CHK_AI0.Checked = CHK_AI1.Checked = CHK_AI2.Checked = true; }
    private void B_MasterAll_Click(object sender, EventArgs e) { foreach (var t in Trainers) t.AI |= 0x7; LoadEntry(); }

    private void B_Clear_Click(object sender, EventArgs e) => SetMoves(new int[4]);
    private void B_CurrentAttack_Click(object sender, EventArgs e) { var m = learn.GetCurrentMoves(CB_Species.SelectedIndex, CB_Forme.SelectedIndex, (int)NUD_Level.Value, 4); SetMoves(m); }
    private void B_HighAttack_Click(object sender, EventArgs e) { TB_ATKIV.Text = "31"; TB_ATKEV.Text = "252"; }
    private void SetMoves(IList<int> moves) { var cbs = new[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4 }; for (int i = 0; i < 4; i++) cbs[i].SelectedIndex = moves[i]; }
    private void CB_Moves_SelectedIndexChanged(object sender, EventArgs e) { CHK_Damage.Enabled = CHK_STAB.Enabled = CB_Moves.SelectedIndex == 1; }
    private void CHK_Damage_CheckedChanged(object sender, EventArgs e) => NUD_Damage.Enabled = CHK_Damage.Checked;
    private void CHK_STAB_CheckedChanged(object sender, EventArgs e) => NUD_STAB.Enabled = CHK_STAB.Checked;
    private void CHK_RandomPKM_CheckedChanged(object sender, EventArgs e) { foreach (CheckBox c in new[] { CHK_G1, CHK_G2, CHK_G3, CHK_G4, CHK_G5, CHK_G6, CHK_G7 }) c.Enabled = CHK_RandomPKM.Checked; }
    private void CHK_RandomClass_CheckedChanged(object sender, EventArgs e) { CHK_IgnoreSpecialClass.Enabled = CHK_RandomClass.Checked; }
    private void CHK_RandomShiny_CheckedChanged(object sender, EventArgs e) => NUD_Shiny.Enabled = CHK_RandomShiny.Checked;
    private void CHK_Level_CheckedChanged(object sender, EventArgs e) => NUD_LevelBoost.Enabled = CHK_Level.Checked;
    private void SyncEVSlider(object sender, EventArgs e) { if (updatingStats || loading) return; var ts = new[] { TB_HPEV_Slider, TB_ATKEV_Slider, TB_DEFEV_Slider, TB_SPAEV_Slider, TB_SPDEV_Slider, TB_SPEEV_Slider }; var tbs = new[] { TB_HPEV, TB_ATKEV, TB_DEFEV, TB_SPAEV, TB_SPDEV, TB_SPEEV }; int i = Array.IndexOf(ts, sender as TrackBar); if (i >= 0) tbs[i].Text = ts[i].Value.ToString(); }

    private void B_ShowdownStorage_Click(object sender, EventArgs e)
    {
        if (currentSlot < 0) { WinFormsUtil.Alert("Select a Pokémon slot first."); return; }
        using var form = new ShowdownSetStorage();
        if (form.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(form.SelectedSet))
        {
            var pk = Trainers[index].Pokemon[currentSlot];
            ParseShowdownSet(form.SelectedSet, pk);
            PopulateFieldsTP7(pk);
            GetQuickFiller(pba[currentSlot], pk);
            WinFormsUtil.Alert("Set applied from storage!");
        }
    }
}