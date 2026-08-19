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
using pk3DS.Core.Structures.PersonalInfo;

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

    /// <summary>
    /// Shows what the trainer's money value works out to as a payout.
    /// </summary>
    private void UpdateMoneyTranslation()
    {
        // The label is built after the trainer list is wired up, so the first selection change can
        // reach here before it exists.
        if (L_MoneyTranslated == null || CB_Money == null) return;
        if (index < 0 || index >= Trainers.Length) return;

        var team = Trainers[index]?.Pokemon;
        if (team == null) return;

        int lv = 0;
        for (int i = 0; i < team.Count; i++)
        {
            // The slot being edited has not been committed back to the team yet, so its level is
            // read from the editor - otherwise the payout only caught up after switching slots.
            int level = i == currentSlot && NUD_Level != null ? (int)NUD_Level.Value : team[i].Level;
            if (level > lv) lv = level;
        }

        int money = CB_Money.SelectedIndex;
        if (money < 0) money = 0;
        L_MoneyTranslated.Text = $"$ {money * lv * 4}";
    }

    private void ApplyUITweaks()
    {
        this.Width = 1180;
        this.Height = 670;
        this.Text = "Trainer Editor";

        if (TC_trdata != null)
        {
            TC_trdata.Left = 10;
            TC_trdata.Top = 10;
            TC_trdata.Width = 390;
            TC_trdata.Height = 440;
            TC_trdata.BringToFront();
        }

        if (B_Randomize != null) B_Randomize.Left = 10;
        if (B_Dump != null) B_Dump.Left = 110;

        Control trainerParent = Tab_Trainer;
        
        // Re-arrange Tab_Trainer controls to fix all overlaps completely
        if (L_numPokemon != null) { L_numPokemon.Parent = trainerParent; L_numPokemon.Location = new Point(10, 22); }
        if (NUD_NumPoke != null) { NUD_NumPoke.Parent = trainerParent; NUD_NumPoke.Location = new Point(65, 20); NUD_NumPoke.Size = new Size(55, 22); }
        if (L_Money != null) { L_Money.Parent = trainerParent; L_Money.Location = new Point(130, 22); }
        if (CB_Money != null) { CB_Money.Parent = trainerParent; CB_Money.Location = new Point(180, 20); CB_Money.Size = new Size(65, 22); }
        if (L_MoneyTranslated != null) { L_MoneyTranslated.Parent = trainerParent; L_MoneyTranslated.Location = new Point(255, 22); }

        if (L_Mode != null) { L_Mode.Parent = trainerParent; L_Mode.Location = new Point(10, 50); }
        if (CB_Mode != null) { CB_Mode.Parent = trainerParent; CB_Mode.Location = new Point(90, 48); CB_Mode.Size = new Size(150, 22); }
        if (CHK_Flag != null) { CHK_Flag.Parent = trainerParent; CHK_Flag.Text = "Master AI"; CHK_Flag.Location = new Point(250, 50); CHK_Flag.Size = new Size(110, 20); }

        if (L_Item_1 != null) { L_Item_1.Parent = trainerParent; L_Item_1.Location = new Point(10, 78); }
        if (CB_Item_1 != null) { CB_Item_1.Parent = trainerParent; CB_Item_1.Location = new Point(55, 76); CB_Item_1.Size = new Size(120, 22); }
        if (L_Item_2 != null) { L_Item_2.Parent = trainerParent; L_Item_2.Location = new Point(185, 78); }
        if (CB_Item_2 != null) { CB_Item_2.Parent = trainerParent; CB_Item_2.Location = new Point(230, 76); CB_Item_2.Size = new Size(130, 22); }

        if (L_Item_3 != null) { L_Item_3.Parent = trainerParent; L_Item_3.Location = new Point(10, 106); }
        if (CB_Item_3 != null) { CB_Item_3.Parent = trainerParent; CB_Item_3.Location = new Point(55, 104); CB_Item_3.Size = new Size(120, 22); }
        if (L_Item_4 != null) { L_Item_4.Parent = trainerParent; L_Item_4.Location = new Point(185, 106); }
        if (CB_Item_4 != null) { CB_Item_4.Parent = trainerParent; CB_Item_4.Location = new Point(230, 104); CB_Item_4.Size = new Size(130, 22); }

        GB_Difficulty = new GroupBox { Text = "Difficulty Bits", Size = new Size(175, 255), Location = new Point(195, 135) };
        B_MaxIVsAll = new Button { Text = "Max IVs All", Size = new Size(155, 25), Location = new Point(10, 25) };
        B_DoublesAll = new Button { Text = "Doubles All", Size = new Size(155, 25), Location = new Point(10, 60) };
        B_PokeChangeAll = new Button { Text = "PokeChange All", Size = new Size(155, 25), Location = new Point(10, 95) };
        Button B_FlagAll = new Button { Text = "Master AI Flag All", Size = new Size(155, 25), Location = new Point(10, 130) };
        B_FlagAll.Click += (s, e) => { foreach (var t in Trainers) t.Flag = true; SaveAllEntries(); LoadEntry(); };

        B_MaxIVsAll.Click += B_MaxIVsAll_Click;
        B_DoublesAll.Click += B_DoublesAll_Click;
        B_PokeChangeAll.Click += B_PokeChangeAll_Click;

        GB_Difficulty.Controls.Add(B_MaxIVsAll);
        GB_Difficulty.Controls.Add(B_DoublesAll);
        GB_Difficulty.Controls.Add(B_PokeChangeAll);
        GB_Difficulty.Controls.Add(B_FlagAll);
        trainerParent.Controls.Add(GB_Difficulty);

        if (GB_AIBits != null) 
        {
            GB_AIBits.Parent = trainerParent;
            GB_AIBits.Location = new Point(10, 135);
            GB_AIBits.Size = new Size(175, 255);
            foreach (var chk in GB_AIBits.Controls.OfType<CheckBox>()) chk.Width = 140;
        }

        // Restore Trainer Name, ID & Class below tab control
        if (TB_TrainerName != null)
        {
            Label L_Name = new Label { Text = "Trainer Name:", Location = new Point(10, 460), AutoSize = true };
            TB_TrainerName.Location = new Point(10, 478);
            TB_TrainerName.Size = new Size(110, 23);
            this.Controls.Add(L_Name);
        }
        if (L_TrainerID != null) { L_TrainerID.Location = new Point(130, 460); }
        if (CB_TrainerID != null) { CB_TrainerID.Location = new Point(130, 478); CB_TrainerID.Size = new Size(125, 23); }
        if (L_Trainer_Class != null) { L_Trainer_Class.Location = new Point(265, 460); }
        if (CB_Trainer_Class != null) { CB_Trainer_Class.Location = new Point(265, 478); CB_Trainer_Class.Size = new Size(135, 23); }

        if (PB_Team1 != null)
        {
            int startX = 420;
            int spacing = PB_Team1.Width + 8;
            PictureBox[] teamBoxes = { PB_Team1, PB_Team2, PB_Team3, PB_Team4, PB_Team5, PB_Team6 };
            for (int i = 0; i < teamBoxes.Length; i++)
                if (teamBoxes[i] != null) { teamBoxes[i].Left = startX + (i * spacing); teamBoxes[i].Top = 10; }
        }

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
        if (Tab_Moves != null) Tab_Moves.Controls.Add(GB_Showdown);

        if (CB_HPType != null)
        {
            CB_HPType.Top = 230; CB_HPType.Left = 110;
            if (CB_HPType.Parent != null)
                foreach (Control l in CB_HPType.Parent.Controls.OfType<Label>().Where(l => l.Text.Contains("Hidden Power")))
                { l.Top = CB_HPType.Top + 4; l.Left = CB_HPType.Left - l.Width - 5; }
        }
        if (B_EnableMega != null) B_EnableMega.Visible = false;
        if (B_EnableZMove != null) B_EnableZMove.Visible = false;

        BuildShowdownCardUI();
    }

    private Panel PNL_ShowdownCard;
    private PictureBox PB_ShowdownSprite;
    private Label L_Type1Badge;
    private Label L_Type2Badge;
    private Label[] L_StatBars;
    private TextBox TB_Nickname;
    private EggMoves[] EggMovesData;
    private Label[] L_BaseVal;
    private Label[] L_BaseBar;
    private Label[] L_TotalStat;
    private Label L_RemainingEVs;
    private Label[] L_StatName;

    private void BuildShowdownCardUI()
    {
        if (TC_trpoke != null) TC_trpoke.Visible = false;

        PNL_ShowdownCard = new Panel
        {
            Location = new Point(420, 68),
            Size = new Size(740, 555),
            BackColor = Color.FromArgb(42, 47, 54),
            ForeColor = Color.White
        };

        // Top Action Bar
        Panel PNL_ActionBar = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(32, 36, 42) };
        
        B_ImportTeam = new Button { Text = "Import Team (Showdown)", Size = new Size(160, 24), Location = new Point(5, 2), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 66, 75) };
        B_ExportTeam = new Button { Text = "Export Team (Showdown)", Size = new Size(160, 24), Location = new Point(170, 2), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 66, 75) };
        Button B_ShowdownStorageBtn = new Button { Text = "📦 Sets Storage", Size = new Size(110, 24), Location = new Point(335, 2), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 66, 75) };
        Button B_CopyCard = new Button { Text = "📋 Copy", Size = new Size(70, 24), Location = new Point(450, 2), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 66, 75) };
        Button B_DeleteCard = new Button { Text = "🗑 Delete", Size = new Size(65, 24), Location = new Point(525, 2), FlatStyle = FlatStyle.Flat, ForeColor = Color.Tomato, BackColor = Color.FromArgb(60, 66, 75) };

        B_ImportTeam.Click += B_ImportTeam_Click;
        B_ExportTeam.Click += B_ExportTeam_Click;
        B_ShowdownStorageBtn.Click += B_ShowdownStorage_Click;
        B_CopyCard.Click += B_ExportSet_Click;
        B_DeleteCard.Click += (s, e) => {
            if (currentSlot >= 0 && currentSlot < Trainers[index].NumPokemon)
            {
                Trainers[index].Pokemon.RemoveAt(currentSlot);
                Trainers[index].NumPokemon = (int)--NUD_NumPoke.Value;
                PopulateTeam(Trainers[index]);
                GetSlotColor(currentSlot, null);
                currentSlot = -1;
            }
        };

        PNL_ActionBar.Controls.Add(B_ImportTeam);
        PNL_ActionBar.Controls.Add(B_ExportTeam);
        PNL_ActionBar.Controls.Add(B_ShowdownStorageBtn);
        PNL_ActionBar.Controls.Add(B_CopyCard);
        PNL_ActionBar.Controls.Add(B_DeleteCard);
        PNL_ShowdownCard.Controls.Add(PNL_ActionBar);

        // Main Card Frame
        Panel PNL_MainCard = new Panel { Location = new Point(5, 33), Size = new Size(730, 485), BackColor = Color.FromArgb(52, 58, 67), BorderStyle = BorderStyle.FixedSingle };

        // 1. Left Column: Nickname -> Species Dropdown -> Form Dropdown -> Sprite
        Label L_Nick = new Label { Text = "Nickname", Location = new Point(8, 5), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        TB_Nickname = new TextBox { Location = new Point(8, 20), Size = new Size(110, 22), BackColor = Color.FromArgb(35, 39, 46), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        
        Label L_Spec = new Label { Text = "Pokémon", Location = new Point(8, 48), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        CB_Species.Parent = PNL_MainCard;
        CB_Species.Location = new Point(8, 64);
        CB_Species.Size = new Size(110, 23);

        Label L_FormeLabel = new Label { Text = "Form:", Location = new Point(8, 92), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        CB_Forme.Parent = PNL_MainCard;
        CB_Forme.Location = new Point(8, 108);
        CB_Forme.Size = new Size(110, 23);

        PB_ShowdownSprite = new PictureBox { Location = new Point(8, 136), Size = new Size(110, 70), SizeMode = PictureBoxSizeMode.CenterImage, BackColor = Color.Transparent };

        PNL_MainCard.Controls.Add(L_Nick);
        PNL_MainCard.Controls.Add(TB_Nickname);
        PNL_MainCard.Controls.Add(L_Spec);
        PNL_MainCard.Controls.Add(L_FormeLabel);
        PNL_MainCard.Controls.Add(PB_ShowdownSprite);

        // 4. Interactive Details Panel (Editable Level, Gender, Happiness, Shiny, HP Type)
        Label L_DetailsHeader = new Label { Text = "Details", Location = new Point(125, 5), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        Panel PNL_EditGrid = new Panel { Location = new Point(125, 20), Size = new Size(310, 80), BackColor = Color.FromArgb(35, 39, 46), BorderStyle = BorderStyle.FixedSingle };
        
        Label L_Lv = new Label { Text = "Lv:", Location = new Point(5, 8), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        NUD_Level.Parent = PNL_EditGrid;
        NUD_Level.Location = new Point(28, 5);
        NUD_Level.Size = new Size(55, 22);
        NUD_Level.ValueChanged += (s, e) => {
            if (!updatingStats && !loading && currentSlot >= 0)
            {
                pkm.Level = (byte)NUD_Level.Value;
                UpdateStats(s, e);
                UpdateMoneyTranslation(); // the payout is derived from the team's highest level
            }
        };

        Label L_Gen = new Label { Text = "Gender:", Location = new Point(90, 8), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        CB_Gender.Parent = PNL_EditGrid;
        CB_Gender.Location = new Point(140, 5);
        CB_Gender.Size = new Size(160, 22);

        Label L_Hap = new Label { Text = "Hap:", Location = new Point(5, 42), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        if (TB_Happiness != null)
        {
            TB_Happiness.Parent = PNL_EditGrid;
            TB_Happiness.Location = new Point(35, 38);
            TB_Happiness.Size = new Size(40, 22);
        }

        CHK_Shiny.Parent = PNL_EditGrid;
        CHK_Shiny.Location = new Point(82, 40);
        CHK_Shiny.Text = "Shiny";
        CHK_Shiny.ForeColor = Color.White;
        CHK_Shiny.AutoSize = true;

        Label L_HP = new Label { Text = "HP Type:", Location = new Point(145, 42), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        CB_HPType.Parent = PNL_EditGrid;
        CB_HPType.Location = new Point(205, 38);
        CB_HPType.Size = new Size(95, 22);

        PNL_EditGrid.Controls.Add(L_Lv);
        PNL_EditGrid.Controls.Add(L_Gen);
        PNL_EditGrid.Controls.Add(L_Hap);
        PNL_EditGrid.Controls.Add(L_HP);

        PNL_MainCard.Controls.Add(L_DetailsHeader);
        PNL_MainCard.Controls.Add(PNL_EditGrid);

        // 5. Dynamic Type Badges
        L_Type1Badge = new Label { Text = "NORMAL", Location = new Point(125, 108), Size = new Size(70, 20), BackColor = Color.Gray, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
        L_Type2Badge = new Label { Text = "FLYING", Location = new Point(200, 108), Size = new Size(70, 20), BackColor = Color.Purple, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
        PNL_MainCard.Controls.Add(L_Type1Badge);
        PNL_MainCard.Controls.Add(L_Type2Badge);

        // 6. Item & Ability Dropdowns
        Label L_Itm = new Label { Text = "Item", Location = new Point(125, 135), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        CB_Item.Parent = PNL_MainCard;
        CB_Item.Location = new Point(125, 152);
        CB_Item.Size = new Size(145, 23);

        Label L_Abil = new Label { Text = "Ability", Location = new Point(280, 135), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        CB_Ability.Parent = PNL_MainCard;
        CB_Ability.Location = new Point(280, 152);
        CB_Ability.Size = new Size(155, 23);
        CB_Ability.SelectedIndexChanged += CB_Ability_SelectedIndexChanged;

        PNL_MainCard.Controls.Add(L_Itm);
        PNL_MainCard.Controls.Add(L_Abil);

        // 7. Moves Box
        Label L_Mvs = new Label { Text = "Moves", Location = new Point(445, 5), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        ComboBox[] mvs = { CB_Move1, CB_Move2, CB_Move3, CB_Move4 };
        for (int i = 0; i < 4; i++)
        {
            mvs[i].Parent = PNL_MainCard;
            mvs[i].Location = new Point(445, 22 + (i * 34));
            mvs[i].Size = new Size(150, 23);
            mvs[i].BackColor = Color.FromArgb(35, 39, 46);
            mvs[i].ForeColor = Color.White;
            mvs[i].DropDownStyle = ComboBoxStyle.DropDownList;
        }
        PNL_MainCard.Controls.Add(L_Mvs);

        // 8. Base Stats Summary Bars (Top Right)
        Label L_Sts = new Label { Text = "Stats", Location = new Point(605, 5), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F) };
        PNL_MainCard.Controls.Add(L_Sts);

        string[] statNames = { "HP", "Atk", "Def", "SpA", "SpD", "Spe" };
        L_StatBars = new Label[6];
        for (int i = 0; i < 6; i++)
        {
            Label lbl = new Label { Text = statNames[i], Location = new Point(605, 23 + (i * 24)), Size = new Size(26, 18), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 7.5F) };
            Label bar = new Label { Location = new Point(633, 25 + (i * 24)), Size = new Size(80, 14), BackColor = Color.LightGreen, BorderStyle = BorderStyle.FixedSingle, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = Color.Black };
            L_StatBars[i] = bar;
            PNL_MainCard.Controls.Add(lbl);
            PNL_MainCard.Controls.Add(bar);
        }

        // 9. Reconstructed Inline EVs / IVs & Nature Teambuilder Panel (Matching Image 2 Layout)
        Panel PNL_EVIVPanel = new Panel { Location = new Point(5, 215), Size = new Size(710, 265), BackColor = Color.FromArgb(38, 43, 51), BorderStyle = BorderStyle.FixedSingle };
        Label L_EVHeader = new Label { Text = "EVs", Location = new Point(10, 5), AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        PNL_EVIVPanel.Controls.Add(L_EVHeader);

        // Column Headers (Base | EVs | IVs)
        Label L_HBase = new Label { Text = "Base", Location = new Point(90, 25), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };
        Label L_HEVs = new Label { Text = "EVs", Location = new Point(230, 25), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
        Label L_HIVs = new Label { Text = "IVs", Location = new Point(540, 25), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
        PNL_EVIVPanel.Controls.Add(L_HBase);
        PNL_EVIVPanel.Controls.Add(L_HEVs);
        PNL_EVIVPanel.Controls.Add(L_HIVs);

        MaskedTextBox[] ivs = { TB_HPIV, TB_ATKIV, TB_DEFIV, TB_SPAIV, TB_SPDIV, TB_SPEIV };
        MaskedTextBox[] evs = { TB_HPEV, TB_ATKEV, TB_DEFEV, TB_SPAEV, TB_SPDEV, TB_SPEEV };
        TrackBar[] evSliders = { TB_HPEV_Slider, TB_ATKEV_Slider, TB_DEFEV_Slider, TB_SPAEV_Slider, TB_SPDEV_Slider, TB_SPEEV_Slider };
        string[] fullStatLabels = { "HP", "Attack", "Defense", "Sp. Atk.", "Sp. Def.", "Speed" };

        L_BaseVal = new Label[6];
        L_BaseBar = new Label[6];
        L_TotalStat = new Label[6];
        L_StatName = new Label[6];

        for (int i = 0; i < 6; i++)
        {
            int rowY = 45 + (i * 28);

            // Stat Name Label
            Label lStatName = new Label { Text = fullStatLabels[i], Location = new Point(10, rowY + 3), Size = new Size(60, 18), ForeColor = Color.White, TextAlign = ContentAlignment.TopRight, Font = new Font("Segoe UI", 8F) };
            L_StatName[i] = lStatName;

            // Base Stat Number & Visual Color Bar
            Label lBaseVal = new Label { Text = "0", Location = new Point(75, rowY + 3), Size = new Size(30, 18), ForeColor = Color.White, Font = new Font("Segoe UI", 8F, FontStyle.Bold), TextAlign = ContentAlignment.TopRight };
            Label lBaseBar = new Label { Location = new Point(110, rowY + 6), Size = new Size(70, 10), BackColor = Color.Gold, BorderStyle = BorderStyle.FixedSingle };
            L_BaseVal[i] = lBaseVal;
            L_BaseBar[i] = lBaseBar;

            // EV Textbox
            evs[i].Parent = PNL_EVIVPanel;
            evs[i].Location = new Point(190, rowY);
            evs[i].Size = new Size(45, 22);
            evs[i].BackColor = Color.FromArgb(28, 32, 38);
            evs[i].ForeColor = Color.White;
            evs[i].BorderStyle = BorderStyle.FixedSingle;

            // EV Slider TrackBar
            TrackBar slider = evSliders[i] ?? new TrackBar();
            slider.Parent = PNL_EVIVPanel;
            slider.Location = new Point(240, rowY - 2);
            slider.AutoSize = false;
            slider.TickStyle = TickStyle.None;
            slider.Size = new Size(280, 22);
            slider.Minimum = 0; slider.Maximum = 252;
            slider.ValueChanged += SyncEVSlider;

            // IV Textbox
            ivs[i].Parent = PNL_EVIVPanel;
            ivs[i].Location = new Point(535, rowY);
            ivs[i].Size = new Size(40, 22);
            ivs[i].BackColor = Color.FromArgb(28, 32, 38);
            ivs[i].ForeColor = Color.White;
            ivs[i].BorderStyle = BorderStyle.FixedSingle;

            // Final Total Calculated Stat
            Label lTotal = new Label { Text = "0", Location = new Point(585, rowY + 3), Size = new Size(45, 18), ForeColor = Color.LightGreen, Font = new Font("Segoe UI", 8F, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter };
            L_TotalStat[i] = lTotal;

            PNL_EVIVPanel.Controls.Add(lStatName);
            PNL_EVIVPanel.Controls.Add(lBaseVal);
            PNL_EVIVPanel.Controls.Add(lBaseBar);
            PNL_EVIVPanel.Controls.Add(lTotal);
        }

        // Footer: Remaining EVs & Nature Dropdown & 510 EV Cap Toggle & Set IVs to Max
        L_RemainingEVs = new Label { Text = "Remaining: 510", Location = new Point(190, 222), AutoSize = true, ForeColor = Color.LightGray, Font = new Font("Segoe UI", 8.5F) };
        PNL_EVIVPanel.Controls.Add(L_RemainingEVs);

        CHK_IgnoreEVCap = new CheckBox
        {
            Text = "Ignore 510 EV Limit",
            Location = new Point(340, 220),
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8F)
        };
        CHK_IgnoreEVCap.CheckedChanged += (s, e) => UpdateStats(s, e);
        PNL_EVIVPanel.Controls.Add(CHK_IgnoreEVCap);

        Button B_SetMaxIVs = new Button
        {
            Text = "Set IVs to Max",
            Location = new Point(480, 218),
            Size = new Size(100, 24),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 66, 75),
            Font = new Font("Segoe UI", 8F)
        };
        B_SetMaxIVs.Click += (s, e) =>
        {
            updatingStats = true;
            TB_HPIV.Text = TB_ATKIV.Text = TB_DEFIV.Text = TB_SPAIV.Text = TB_SPDIV.Text = TB_SPEIV.Text = "31";
            updatingStats = false;
            UpdateStats(s, e);
        };
        PNL_EVIVPanel.Controls.Add(B_SetMaxIVs);

        Label L_Nat = new Label { Text = "Nature:", Location = new Point(10, 222), AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 8.5F) };
        CB_Nature.Parent = PNL_EVIVPanel;
        CB_Nature.Location = new Point(60, 220);
        CB_Nature.Size = new Size(120, 22);
        PNL_EVIVPanel.Controls.Add(L_Nat);

        PNL_MainCard.Controls.Add(PNL_EVIVPanel);
        PNL_ShowdownCard.Controls.Add(PNL_MainCard);
        this.Controls.Add(PNL_ShowdownCard);
        PNL_ShowdownCard.BringToFront();
    }

    private CheckBox CHK_IgnoreEVCap;

    private static readonly int[] TM_MOVE_IDS = {
        526, 337, 473, 347, 46, 92, 258, 339, 599, 237, 241, 269, 58, 59, 63, 113, 182, 240, 355, 219,
        218, 76, 479, 85, 87, 89, 216, 141, 94, 247, 280, 104, 115, 482, 53, 188, 201, 126, 317, 332,
        260, 263, 488, 156, 213, 168, 490, 496, 497, 315, 211, 411, 412, 206, 503, 374, 451, 492, 693, 511,
        261, 512, 373, 153, 421, 371, 684, 416, 397, 694, 444, 521, 86, 360, 14, 19, 129, 523, 524, 157,
        404, 525, 611, 398, 138, 447, 207, 214, 369, 164, 430, 433, 528, 57, 555, 267, 399, 127, 605, 590
    };

    private static readonly Dictionary<int, int> TUTOR_FLAG_MOVES = new Dictionary<int, int>
    {
        // Special / Type Tutors (Bits 0..10 at offset 0x38)
        { 0, 517 }, { 1, 518 }, { 2, 519 }, { 3, 338 }, { 4, 307 }, { 5, 308 }, { 6, 434 }, { 7, 620 }, { 8, 344 }, { 9, 548 }, { 10, 547 },
    };

    private int[] _dynamicTutors;
    private int[] DynamicTutors
    {
        get
        {
            if (_dynamicTutors == null)
            {
                var data = TutorEditor7.GetUSUMTutorData(Path.Combine(Main.RomFSPath, "Shop.cro"), TutorEditor7.Tutors_USUM);
                _dynamicTutors = data.moves;
            }
            return _dynamicTutors;
        }
    }


    private void UpdateMoveDropdowns(int sp, int fm)
    {
        int formIdx = Main.Config.Personal.GetFormIndex(sp, fm);
        HashSet<int> legalMoves = new HashSet<int>();

        // 1. Level-Up Moves
        if (Main.Config.Learnsets != null && formIdx < Main.Config.Learnsets.Length)
        {
            var ls = Main.Config.Learnsets[formIdx];
            if (ls != null && ls.Moves != null)
                foreach (int m in ls.Moves) if (m > 0 && m < movelist.Length) legalMoves.Add(m);
        }

        // 2. TM & Tutor Moves
        if (formIdx < Main.SpeciesStat.Length)
        {
            var p = Main.SpeciesStat[formIdx];
            if (p != null && p.TMHM != null)
            {
                for (int i = 0; i < p.TMHM.Length && i < TM_MOVE_IDS.Length; i++)
                    if (p.TMHM[i]) legalMoves.Add(TM_MOVE_IDS[i]);
            }
            if (p is PersonalInfoSM pSM && pSM.TutorFlags != null)
            {
                for (int i = 0; i < pSM.TutorFlags.Length; i++)
                {
                    if (!pSM.TutorFlags[i]) continue;

                    if (TUTOR_FLAG_MOVES.TryGetValue(i, out int moveID))
                    {
                        legalMoves.Add(moveID);
                    }
                    else if (i >= 32 && i - 32 < DynamicTutors.Length)
                    {
                        int dynMove = DynamicTutors[i - 32];
                        if (dynMove > 0) legalMoves.Add(dynMove);
                    }
                }
            }
        }

        // 3. Egg Moves
        if (EggMovesData != null && formIdx < EggMovesData.Length)
        {
            var em = EggMovesData[formIdx];
            if (em != null && em.Moves != null)
                foreach (int m in em.Moves) if (m > 0 && m < movelist.Length) legalMoves.Add(m);
        }

        // 4. Preserve current moves
        int[] cur = { pkm != null ? pkm.Move1 : 0, pkm != null ? pkm.Move2 : 0, pkm != null ? pkm.Move3 : 0, pkm != null ? pkm.Move4 : 0 };
        foreach (int m in cur) if (m > 0 && m < movelist.Length) legalMoves.Add(m);

        List<MoveComboItem> items = new List<MoveComboItem> { new MoveComboItem { Name = "(None)", ID = 0 } };
        items.AddRange(legalMoves.OrderBy(m => movelist[m]).Select(m => new MoveComboItem { Name = movelist[m], ID = m }));

        ComboBox[] cbs = { CB_Move1, CB_Move2, CB_Move3, CB_Move4 };
        for (int i = 0; i < 4; i++)
        {
            cbs[i].BeginUpdate();
            cbs[i].DataSource = items.ToList();
            cbs[i].DisplayMember = "Name";
            cbs[i].ValueMember = "ID";
            SetComboMoveValue(cbs[i], cur[i]);
            cbs[i].EndUpdate();
        }
    }

    public class MoveComboItem
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public override string ToString() => Name;
    }

    private static void SetComboMoveValue(ComboBox cb, int moveID)
    {
        if (cb.DataSource is List<MoveComboItem> list)
        {
            int idx = list.FindIndex(x => x.ID == moveID);
            cb.SelectedIndex = idx >= 0 ? idx : 0;
        }
    }

    private static int GetComboMoveValue(ComboBox cb)
    {
        if (cb.SelectedItem is MoveComboItem item) return item.ID;
        return 0;
    }

    private static Color GetTypeColor(int typeIndex)
    {
        return typeIndex switch
        {
            0 => Color.FromArgb(168, 168, 120),  // Normal
            1 => Color.FromArgb(192, 48, 40),    // Fighting
            2 => Color.FromArgb(168, 144, 240),  // Flying
            3 => Color.FromArgb(160, 64, 160),   // Poison
            4 => Color.FromArgb(224, 192, 104),  // Ground
            5 => Color.FromArgb(184, 160, 56),   // Rock
            6 => Color.FromArgb(168, 184, 32),   // Bug
            7 => Color.FromArgb(112, 88, 152),   // Ghost
            8 => Color.FromArgb(184, 184, 208),  // Steel
            9 => Color.FromArgb(240, 128, 48),   // Fire
            10 => Color.FromArgb(104, 144, 240), // Water
            11 => Color.FromArgb(120, 200, 80),  // Grass
            12 => Color.FromArgb(248, 208, 48),  // Electric
            13 => Color.FromArgb(248, 88, 136),  // Psychic
            14 => Color.FromArgb(152, 216, 216), // Ice
            15 => Color.FromArgb(112, 56, 248),  // Dragon
            16 => Color.FromArgb(112, 88, 72),   // Dark
            17 => Color.FromArgb(238, 153, 172), // Fairy
            _ => Color.FromArgb(104, 104, 104)
        };
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
            if (slot < Trainers[index].NumPokemon)
            {
                var pk = Trainers[index].Pokemon[slot];
                currentSlot = slot;
                try { PopulateFieldsTP7(pk); } catch { }
                GetSlotColor(slot, Properties.Resources.slotView);
            }
            else if (slot == Trainers[index].NumPokemon && slot < 6)
            {
                if (CB_Species.SelectedIndex == 0) { WinFormsUtil.Alert("Can't set empty slot."); return; }
                if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Add this Pokémon to the team?") != DialogResult.Yes) return;
                currentSlot = slot;
                var pk = PrepareTP7(); Trainers[index].Pokemon.Add(pk); Trainers[index].NumPokemon = (int)++NUD_NumPoke.Value;
                GetQuickFiller(pba[slot], pk); GetSlotColor(slot, Properties.Resources.slotView);
            }
        }
    }

    private void PopulateTeam(TrainerData7 tr)
    {
        int savedSlot = currentSlot;
        currentSlot = -1;
        for (int i = 0; i < tr.NumPokemon; i++) GetQuickFiller(pba[i], tr.Pokemon[i]);
        for (int i = tr.NumPokemon; i < 6; i++) pba[i].Image = null;
        currentSlot = savedSlot;
    }
    private void GetSlotColor(int slot, Image color) { foreach (var t in pba) t.BackgroundImage = null; if (slot >= 0 && slot < pba.Length) pba[slot].BackgroundImage = color; }

    private static void GetQuickFiller(PictureBox pb, TrainerPoke7 pk)
    {
        Bitmap rawImg = WinFormsUtil.GetSprite(pk.Species, pk.Form, pk.Gender, pk.Item, Main.Config, pk.Shiny);
        pb.Image = WinFormsUtil.ScaleImage(rawImg, 2);
    }

    private void UpdateSlotSprite()
    {
        int sp = CB_Species.SelectedIndex;
        int fm = CB_Forme.SelectedIndex >= 0 ? CB_Forme.SelectedIndex : 0;
        int gn = CB_Gender.SelectedIndex >= 0 ? CB_Gender.SelectedIndex : 0;
        int it = CB_Item.SelectedIndex >= 0 ? CB_Item.SelectedIndex : 0;
        bool sh = CHK_Shiny.Checked;

        Bitmap rawImg = WinFormsUtil.GetSprite(sp, fm, gn, it, Main.Config, sh);
        Bitmap scaledImg = WinFormsUtil.ScaleImage(rawImg, 2);

        if (PB_ShowdownSprite != null)
            PB_ShowdownSprite.Image = scaledImg;

        if (currentSlot >= 0 && currentSlot < pba.Length)
        {
            pba[currentSlot].Image = scaledImg;
        }
    }

    private void RefreshFormAbility(object sender, EventArgs e) { if (index < 0 || loading || updatingStats) return; if (CB_Forme.SelectedIndex >= 0) pkm.Form = CB_Forme.SelectedIndex; RefreshPKMSlotAbility(); }
    private void RefreshSpeciesAbility(object sender, EventArgs e) { if (index < 0 || loading || updatingStats) return; pkm.Species = (ushort)CB_Species.SelectedIndex; FormUtil.SetForms(CB_Species.SelectedIndex, CB_Forme, AltForms); if (CB_Forme.SelectedIndex >= 0) pkm.Form = CB_Forme.SelectedIndex; RefreshPKMSlotAbility(); }

    private void RefreshPKMSlotAbility()
    {
        int pr = CB_Ability.SelectedIndex; int sp = CB_Species.SelectedIndex; int fm = CB_Forme.SelectedIndex >= 0 ? CB_Forme.SelectedIndex : 0;
        if (sp >= Main.SpeciesStat.Length) return;
        int formIdx = Main.Config.Personal.GetFormIndex(sp, fm);
        
        CB_Ability.Items.Clear();
        CB_Ability.Items.Add("Any (1 or 2)");
        CB_Ability.Items.Add(AnyIncludingHiddenText);
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[formIdx].Abilities[0]] + " (1)");
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[formIdx].Abilities[1]] + " (2)");
        CB_Ability.Items.Add(abilitylist[Main.SpeciesStat[formIdx].Abilities[2]] + " (H)");
        CB_Ability.SelectedIndex = Math.Min(Math.Max(0, pr), CB_Ability.Items.Count - 1);

        UpdateTypeBadges(formIdx);
        UpdateMoveDropdowns(sp, fm);
        UpdateSlotSprite();
    }

    private const string AnyIncludingHiddenText = "Any (1, 2, and H)";

    /// <summary>Position of the convenience entry, which is not a value the ROM can store.</summary>
    private const int AbilityAnyIncludingHiddenIndex = 1;

    /// <summary>First list position that maps to a real ability slot.</summary>
    private const int AbilityFirstConcreteIndex = 2;

    /// <summary>
    /// List position to the raw 2-bit trainer field. The list carries one entry the ROM has no
    /// value for, so past it the two run one apart: 2/3/4 are ability 1, 2 and Hidden.
    /// </summary>
    private static int AbilityIndexToSlot(int index) =>
        index < AbilityFirstConcreteIndex ? 0 : index - 1;

    /// <summary>The inverse, used when loading a Pokémon back into the editor.</summary>
    private static int AbilitySlotToIndex(int slot) =>
        slot <= 0 ? 0 : slot + 1;

    private void CB_Ability_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (loading || index < 0) return;
        if (CB_Ability.SelectedIndex != AbilityAnyIncludingHiddenIndex) return;

        var candidates = new List<int>();
        for (int i = AbilityFirstConcreteIndex; i < CB_Ability.Items.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(CB_Ability.Items[i]?.ToString()))
                candidates.Add(i);
        }
        int rolled = candidates.Count > 0 ? candidates[Util.Rand.Next(candidates.Count)] : AbilityFirstConcreteIndex;
        CB_Ability.SelectedIndex = rolled;
    }

    private void UpdateTypeBadges(int formIdx)
    {
        if (L_Type1Badge == null || L_Type2Badge == null) return;
        var p = Main.SpeciesStat[formIdx];
        int t1 = p.Types[0];
        int t2 = p.Types[1];

        L_Type1Badge.Text = types.Length > t1 ? types[t1].ToUpper() : "NORMAL";
        L_Type1Badge.BackColor = GetTypeColor(t1);
        L_Type1Badge.Visible = true;

        if (t1 != t2 && t2 >= 0 && types.Length > t2)
        {
            L_Type2Badge.Text = types[t2].ToUpper();
            L_Type2Badge.BackColor = GetTypeColor(t2);
            L_Type2Badge.Visible = true;
        }
        else
        {
            L_Type2Badge.Visible = false;
        }
    }

    private static string GetEntryTitle(string str, int i) => $"{str} - {i:000}";

    private void Setup()
    {
        AltForms = Main.Config.Personal.GetFormList(specieslist, Main.Config.MaxSpeciesID);
        CB_TrainerID.Items.Clear(); for (int i = 0; i < trdata.Length; i++) CB_TrainerID.Items.Add(GetEntryTitle(trName[i] ?? "UNKNOWN", i));
        CB_Trainer_Class.Items.Clear(); for (int i = 0; i < trClass.Length; i++) CB_Trainer_Class.Items.Add(GetEntryTitle(trClass[i], i));
        Trainers[0] = new TrainerData7(); for (int i = 1; i < trdata.Length; i++) Trainers[i] = new TrainerData7(trdata[i], trpoke[i]) { Name = trName[i], ID = i };
        specieslist[0] = "---"; abilitylist[0] = itemlist[0] = movelist[0] = "(None)";
        pba = [PB_Team1, PB_Team2, PB_Team3, PB_Team4, PB_Team5, PB_Team6]; foreach (var pb in pba) pb.MouseClick += ClickSlot;
        AIBits = [CHK_AI0, CHK_AI1, CHK_AI2, CHK_AI3, CHK_AI4, CHK_AI5, CHK_AI6, CHK_AI7];
        CB_Species.Items.Clear(); CB_Species.Items.AddRange(specieslist);
        
        if (EggMovesData == null)
        {
            var eggGarc = Main.Config.GetGARCData("eggmove");
            if (eggGarc != null && eggGarc.Files != null)
                EggMovesData = EggMoves7.GetArray(eggGarc.Files);
        }

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

    /// <summary>
    /// Serialises every trainer back into the arrays that get written to the ROM.
    /// </summary>
    private void SaveAllEntries()
    {
        for (int i = 0; i < Trainers.Length; i++)
        {
            if (Trainers[i] != null)
                SaveData(Trainers[i], i);
        }
    }

    private void LoadEntry()
    {
        index = CB_TrainerID.SelectedIndex;
        var tr = Trainers[index];
        loading = true;
        TB_TrainerName.Text = TrainerNames[index];
        PopulateFieldsTD7(tr);
        loading = false;

        UpdateMoneyTranslation();
    }

    private bool loading;
    private TrainerPoke7 pkm;

    private void PopulateFieldsTP7(TrainerPoke7 pk)
    {
        pkm = pk.Clone();
        updatingStats = true;
        CB_Species.SelectedIndex = Math.Min(pkm.Species, CB_Species.Items.Count - 1);
        FormUtil.SetForms(pkm.Species, CB_Forme, AltForms);
        if (pkm.Form >= 0 && pkm.Form < CB_Forme.Items.Count)
            CB_Forme.SelectedIndex = pkm.Form;
        else
            CB_Forme.SelectedIndex = 0;
        pkm.Form = CB_Forme.SelectedIndex >= 0 ? CB_Forme.SelectedIndex : 0;

        CB_Ability.SelectedIndex = Math.Min(AbilitySlotToIndex(pkm.Ability), CB_Ability.Items.Count - 1);
        CB_Item.SelectedIndex = Math.Min(pkm.Item, CB_Item.Items.Count - 1);
        CHK_Shiny.Checked = pkm.Shiny;
        CB_Gender.SelectedIndex = Math.Min(pkm.Gender, CB_Gender.Items.Count - 1);
        
        UpdateMoveDropdowns(pkm.Species, pkm.Form);
        SetComboMoveValue(CB_Move1, pkm.Move1);
        SetComboMoveValue(CB_Move2, pkm.Move2);
        SetComboMoveValue(CB_Move3, pkm.Move3);
        SetComboMoveValue(CB_Move4, pkm.Move4);

        CB_Nature.SelectedIndex = Math.Min(pkm.Nature, CB_Nature.Items.Count - 1);
        NUD_Level.Value = Math.Max(1, Math.Min(NUD_Level.Maximum, (decimal)pkm.Level));
        TB_HPIV.Text = pkm.IV_HP.ToString(); TB_ATKIV.Text = pkm.IV_ATK.ToString(); TB_DEFIV.Text = pkm.IV_DEF.ToString();
        TB_SPAIV.Text = pkm.IV_SPA.ToString(); TB_SPEIV.Text = pkm.IV_SPE.ToString(); TB_SPDIV.Text = pkm.IV_SPD.ToString();
        TB_HPEV.Text = pkm.EV_HP.ToString(); TB_ATKEV.Text = pkm.EV_ATK.ToString(); TB_DEFEV.Text = pkm.EV_DEF.ToString();
        TB_SPAEV.Text = pkm.EV_SPA.ToString(); TB_SPEEV.Text = pkm.EV_SPE.ToString(); TB_SPDEV.Text = pkm.EV_SPD.ToString();
        if (TB_Happiness != null) TB_Happiness.Text = pkm.Friendship.ToString();
        var ts = new TrackBar[] { TB_HPEV_Slider, TB_ATKEV_Slider, TB_DEFEV_Slider, TB_SPAEV_Slider, TB_SPDEV_Slider, TB_SPEEV_Slider };
        var tbs = new MaskedTextBox[] { TB_HPEV, TB_ATKEV, TB_DEFEV, TB_SPAEV, TB_SPDEV, TB_SPEEV };
        for(int i = 0; i < 6; i++) if (ts[i] != null) { int val = WinFormsUtil.ToInt32(tbs[i]); ts[i].Value = val > 252 ? 252 : val; }
        updatingStats = false;

        RefreshPKMSlotAbility();
        UpdateStats(null, null);
    }

    private TrainerPoke7 PrepareTP7()
    {
        var pk = pkm.Clone(); pk.Species = CB_Species.SelectedIndex; pk.Form = CB_Forme.SelectedIndex >= 0 ? CB_Forme.SelectedIndex : 0; pk.Level = (byte)NUD_Level.Value;
        pk.Ability = AbilityIndexToSlot(CB_Ability.SelectedIndex); pk.Item = CB_Item.SelectedIndex; pk.Shiny = CHK_Shiny.Checked; pk.Nature = CB_Nature.SelectedIndex; pk.Gender = CB_Gender.SelectedIndex;
        pk.Move1 = GetComboMoveValue(CB_Move1);
        pk.Move2 = GetComboMoveValue(CB_Move2);
        pk.Move3 = GetComboMoveValue(CB_Move3);
        pk.Move4 = GetComboMoveValue(CB_Move4);
        pk.EV_HP = WinFormsUtil.ToInt32(TB_HPEV); pk.EV_ATK = WinFormsUtil.ToInt32(TB_ATKEV); pk.EV_DEF = WinFormsUtil.ToInt32(TB_DEFEV);
        pk.EV_SPA = WinFormsUtil.ToInt32(TB_SPAEV); pk.EV_SPE = WinFormsUtil.ToInt32(TB_SPEEV); pk.EV_SPD = WinFormsUtil.ToInt32(TB_SPDEV);
        pk.IV_HP = WinFormsUtil.ToInt32(TB_HPIV); pk.IV_ATK = WinFormsUtil.ToInt32(TB_ATKIV); pk.IV_DEF = WinFormsUtil.ToInt32(TB_DEFIV);
        pk.IV_SPA = WinFormsUtil.ToInt32(TB_SPAIV); pk.IV_SPE = WinFormsUtil.ToInt32(TB_SPEIV); pk.IV_SPD = WinFormsUtil.ToInt32(TB_SPDIV);
        if (TB_Happiness != null) pk.Friendship = WinFormsUtil.ToInt32(TB_Happiness);
        return pk;
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
        // SaveEntry covers the visible trainer; SaveAllEntries catches anything edited in memory by
        // a bulk action, so nothing depends on which trainer happened to be selected at close.
        SaveEntry(); SaveAllEntries();
        if (TrainerNames.Modified) Main.Config.SetText(TextName.TrainerNames, TrainerNames.Lines);
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
        int sp = CB_Species.SelectedIndex; int formIdx = Main.Config.Personal.GetFormIndex(sp, CB_Forme.SelectedIndex); var p = Main.SpeciesStat[formIdx]; int lv = (int)NUD_Level.Value; int na = CB_Nature.SelectedIndex;
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

        if (L_BaseVal != null && L_BaseVal.Length == 6)
        {
            int[] baseStats = p.Stats; // HP, Atk, Def, Spe, SpA, SpD
            ushort[] calcStats = { st[0], st[1], st[2], st[4], st[5], st[3] };
            int[] statOrder = { 0, 1, 2, 4, 5, 3 }; // HP, Atk, Def, SpA, SpD, Spe
            
            int evSum = 0;
            for (int i = 0; i < 6; i++)
            {
                int bIdx = statOrder[i];
                int bVal = baseStats[bIdx];
                L_BaseVal[i].Text = bVal.ToString();
                L_BaseBar[i].Width = Math.Min(70, Math.Max(4, bVal / 3));
                L_BaseBar[i].BackColor = bVal >= 130 ? Color.DeepSkyBlue : bVal >= 90 ? Color.LightGreen : bVal >= 60 ? Color.Gold : Color.Orange;
                
                L_TotalStat[i].Text = calcStats[i].ToString();
                evSum += WinFormsUtil.ToInt32(tb_ev[i]);
            }
            if (L_RemainingEVs != null)
            {
                bool ignoreCap = CHK_IgnoreEVCap != null && CHK_IgnoreEVCap.Checked;
                L_RemainingEVs.Text = ignoreCap ? "Remaining: ∞" : $"Remaining: {Math.Max(0, 510 - evSum)}";
            }

            if (L_StatName != null && L_StatName.Length == 6)
            {
                string[] fullStatLabels = { "HP", "Attack", "Defense", "Sp. Atk.", "Sp. Def.", "Speed" };
                int[] rowStatIndices = { 0, 1, 2, 4, 5, 3 };
                int upStat = (na / 5) + 1;
                int downStat = (na % 5) + 1;

                for (int i = 0; i < 6; i++)
                {
                    int statIdx = rowStatIndices[i];
                    if (upStat != downStat && statIdx == upStat)
                    {
                        L_StatName[i].Text = fullStatLabels[i] + " ▲";
                        L_StatName[i].ForeColor = Color.FromArgb(255, 130, 130);
                    }
                    else if (upStat != downStat && statIdx == downStat)
                    {
                        L_StatName[i].Text = fullStatLabels[i] + " ▼";
                        L_StatName[i].ForeColor = Color.FromArgb(130, 180, 255);
                    }
                    else
                    {
                        L_StatName[i].Text = fullStatLabels[i];
                        L_StatName[i].ForeColor = Color.White;
                    }
                }
            }
        }

        var ivs = tb_iv.Select(tb => WinFormsUtil.ToInt32(tb) & 1).ToArray(); updatingStats = true; CB_HPType.SelectedIndex = 15 * (ivs[0] + (2 * ivs[1]) + (4 * ivs[2]) + (8 * ivs[3]) + (16 * ivs[4]) + (32 * ivs[5])) / 63; updatingStats = false;

        if (L_StatBars != null && L_StatBars.Length == 6)
        {
            ushort[] displayStats = { st[0], st[1], st[2], st[4], st[5], st[3] };
            for (int i = 0; i < 6; i++)
            {
                ushort val = displayStats[i];
                string text = val.ToString();
                L_StatBars[i].Text = text;

                int needed = TextRenderer.MeasureText(text, L_StatBars[i].Font).Width + 6;
                L_StatBars[i].Width = Math.Min(80, Math.Max(needed, val / 4));
                L_StatBars[i].BackColor = val >= 300 ? Color.FromArgb(70, 200, 250) : val >= 200 ? Color.FromArgb(120, 220, 100) : val >= 100 ? Color.FromArgb(240, 220, 80) : Color.FromArgb(250, 150, 80);
                L_StatBars[i].ForeColor = Color.Black;
            }
        }
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
        CB_TrainerID.SelectedIndex = 0; var rnd = new SpeciesRandomizer(Main.Config) { G1 = CHK_G1.Checked, G2 = CHK_G2.Checked, G3 = CHK_G3.Checked, G4 = CHK_G4.Checked, G5 = CHK_G5.Checked, G6 = CHK_G6.Checked, G7 = CHK_G7.Checked, G8 = CHK_G8.Checked, G9 = CHK_G9.Checked, E = CHK_E.Checked, L = CHK_L.Checked, rBST = CHK_BST.Checked }; rnd.Initialize();
        var move = new MoveRandomizer(Main.Config) { rSTABCount = (int)NUD_STAB.Value, rDMGCount = (int)NUD_Damage.Value };
        var items = Randomizer.GetRandomItemList();
        for (int i = 0; i < Trainers.Length; i++)
        {
            var tr = Trainers[i]; if (tr.Pokemon.Count == 0) continue;
            foreach (var pk in tr.Pokemon) { if (CHK_RandomPKM.Checked) pk.Species = rnd.GetRandomSpecies(pk.Species); if (CHK_RandomShiny.Checked) pk.Shiny = (int)Util.Random32() % 100 < NUD_Shiny.Value; pk.Moves = move.GetRandomMoveset(pk.Species); }
        }
        LoadEntry(); WinFormsUtil.Alert("Done!");
    }

    private void B_ImportSet_Click(object sender, EventArgs e)
    {
        if (currentSlot < 0 || currentSlot >= Trainers[index].Pokemon.Count)
        {
            WinFormsUtil.Alert("Please select a Pokémon slot first.");
            return;
        }
        string text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            WinFormsUtil.Alert("Clipboard is empty or does not contain text.");
            return;
        }
        var pk = Trainers[index].Pokemon[currentSlot];
        ParseShowdownSet(text, pk);
        PopulateFieldsTP7(pk);
        GetQuickFiller(pba[currentSlot], pk);
        WinFormsUtil.Alert($"Imported Showdown set for {specieslist[pk.Species]}!");
    }

    private void B_ImportTeam_Click(object sender, EventArgs e)
    {
        string text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            WinFormsUtil.Alert("Clipboard does not contain text. Please copy a Showdown team first.");
            return;
        }

        List<string> setBlocks = new List<string>();
        StringBuilder currentBlock = new StringBuilder();
        foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentBlock.Length > 0)
                {
                    setBlocks.Add(currentBlock.ToString());
                    currentBlock.Clear();
                }
            }
            else
            {
                currentBlock.AppendLine(line);
            }
        }
        if (currentBlock.Length > 0) setBlocks.Add(currentBlock.ToString());

        if (setBlocks.Count == 0)
        {
            WinFormsUtil.Alert("Could not parse any Showdown sets from clipboard.");
            return;
        }

        var tr = Trainers[index];
        tr.Pokemon.Clear();
        int count = Math.Min(6, setBlocks.Count);
        for (int i = 0; i < count; i++)
        {
            var pk = new TrainerPoke7();
            ParseShowdownSet(setBlocks[i], pk);
            tr.Pokemon.Add(pk);
        }
        tr.NumPokemon = count;
        NUD_NumPoke.Value = count;
        PopulateTeam(tr);
        if (count > 0)
        {
            currentSlot = 0;
            PopulateFieldsTP7(tr.Pokemon[0]);
            GetSlotColor(0, Properties.Resources.slotView);
        }
        else
        {
            currentSlot = -1;
            GetSlotColor(-1, null);
        }
        WinFormsUtil.Alert($"Imported team of {count} Pokémon into trainer roster!");
    }

    private void ResolveSpeciesAndForm(string inputName, out ushort speciesIdx, out byte formIdx)
    {
        speciesIdx = 0;
        formIdx = 0;
        if (string.IsNullOrWhiteSpace(inputName)) return;

        string cleanName = inputName.Trim();

        if (cleanName.Equals("Nidoran-F", StringComparison.OrdinalIgnoreCase) || cleanName.Equals("NidoranF", StringComparison.OrdinalIgnoreCase))
            cleanName = "Nidoran♀";
        else if (cleanName.Equals("Nidoran-M", StringComparison.OrdinalIgnoreCase) || cleanName.Equals("NidoranM", StringComparison.OrdinalIgnoreCase))
            cleanName = "Nidoran♂";
        else if (cleanName.Equals("Flabebe", StringComparison.OrdinalIgnoreCase))
            cleanName = "Flabébé";
        else if (cleanName.StartsWith("Flabebe-", StringComparison.OrdinalIgnoreCase))
            cleanName = "Flabébé" + cleanName.Substring(7);

        int directIdx = Array.FindIndex(specieslist, x => x != null && x.Equals(cleanName, StringComparison.OrdinalIgnoreCase));
        if (directIdx >= 0)
        {
            speciesIdx = (ushort)directIdx;
            formIdx = 0;
            return;
        }

        string baseName = cleanName;
        string formName = string.Empty;

        int dashIdx = cleanName.IndexOf('-');
        if (dashIdx > 0)
        {
            baseName = cleanName.Substring(0, dashIdx).Trim();
            formName = cleanName.Substring(dashIdx + 1).Trim();
        }

        if (baseName.Equals("NidoranF", StringComparison.OrdinalIgnoreCase)) baseName = "Nidoran♀";
        if (baseName.Equals("NidoranM", StringComparison.OrdinalIgnoreCase)) baseName = "Nidoran♂";
        if (baseName.Equals("Flabebe", StringComparison.OrdinalIgnoreCase)) baseName = "Flabébé";

        int baseIdx = Array.FindIndex(specieslist, x => x != null && x.Equals(baseName, StringComparison.OrdinalIgnoreCase));
        if (baseIdx < 0)
        {
            string altBase = baseName.Replace('-', ' ');
            baseIdx = Array.FindIndex(specieslist, x => x != null && x.Equals(altBase, StringComparison.OrdinalIgnoreCase));
            if (baseIdx < 0)
            {
                altBase = baseName.Replace(" ", "");
                baseIdx = Array.FindIndex(specieslist, x => x != null && x.Equals(altBase, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (baseIdx >= 0)
        {
            speciesIdx = (ushort)baseIdx;
            if (!string.IsNullOrWhiteSpace(formName) && AltForms != null && baseIdx < AltForms.Length && AltForms[baseIdx] != null)
            {
                string[] forms = AltForms[baseIdx];
                for (int f = 0; f < forms.Length; f++)
                {
                    if (string.IsNullOrWhiteSpace(forms[f])) continue;
                    string normalizedForm = forms[f].Replace(" ", "").Replace("-", "");
                    string targetForm = formName.Replace(" ", "").Replace("-", "");
                    if (forms[f].Equals(formName, StringComparison.OrdinalIgnoreCase) ||
                        normalizedForm.Equals(targetForm, StringComparison.OrdinalIgnoreCase) ||
                        forms[f].EndsWith(formName, StringComparison.OrdinalIgnoreCase))
                    {
                        formIdx = (byte)f;
                        break;
                    }
                }
            }
        }
    }

    private int MatchItem(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        int idx = Array.FindIndex(itemlist, x => x != null && x.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) return idx;
        string alt = name.Replace("-", " ");
        idx = Array.FindIndex(itemlist, x => x != null && x.Equals(alt, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) return idx;
        alt = name.Replace(" ", "");
        return Array.FindIndex(itemlist, x => x != null && x.Replace(" ", "").Replace("-", "").Equals(alt, StringComparison.OrdinalIgnoreCase));
    }

    private int MatchMove(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        int idx = Array.FindIndex(movelist, x => x != null && x.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) return idx;
        string alt = name.Replace("-", " ");
        idx = Array.FindIndex(movelist, x => x != null && x.Equals(alt, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) return idx;
        alt = name.Replace(" ", "");
        return Array.FindIndex(movelist, x => x != null && x.Replace(" ", "").Replace("-", "").Equals(alt, StringComparison.OrdinalIgnoreCase));
    }

    private byte ResolveAbilitySlot(int species, int form, string abilityName)
    {
        if (string.IsNullOrWhiteSpace(abilityName)) return 0;
        int formIdx = Main.Config.Personal.GetFormIndex(species, form);
        if (formIdx >= Main.SpeciesStat.Length) return 0;

        var pStat = Main.SpeciesStat[formIdx];
        int ab1 = pStat.Abilities[0];
        int ab2 = pStat.Abilities[1];
        int abH = pStat.Abilities[2];

        if (ab1 < abilitylist.Length && abilitylist[ab1].Equals(abilityName, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (ab2 < abilitylist.Length && abilitylist[ab2].Equals(abilityName, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (abH < abilitylist.Length && abilitylist[abH].Equals(abilityName, StringComparison.OrdinalIgnoreCase))
            return 3;

        int rawIdx = Array.FindIndex(abilitylist, x => x != null && x.Equals(abilityName, StringComparison.OrdinalIgnoreCase));
        if (rawIdx >= 0)
        {
            if (rawIdx == ab1) return 1;
            if (rawIdx == ab2) return 2;
            if (rawIdx == abH) return 3;
        }

        return 0;
    }

    private void ParseShowdownSet(string set, TrainerPoke7 pk)
    {
        if (string.IsNullOrWhiteSpace(set)) return;
        string[] lines = set.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return;

        int hpiv = 31, atkiv = 31, defiv = 31, spaiv = 31, spdiv = 31, speiv = 31;
        int hpev = 0, atkev = 0, defev = 0, spaev = 0, spdev = 0, speev = 0;
        int moveIdx = 0;
        int[] moves = new int[4];
        string abilityNameStr = null;
        bool levelSpecified = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].Trim();
            if (i == 0)
            {
                string speciesLine = l;
                string itemStr = null;
                int atIdx = l.IndexOf('@');
                if (atIdx >= 0)
                {
                    speciesLine = l.Substring(0, atIdx).Trim();
                    itemStr = l.Substring(atIdx + 1).Trim();
                }

                byte gender = 0;
                if (speciesLine.EndsWith("(M)", StringComparison.OrdinalIgnoreCase))
                {
                    gender = 1;
                    speciesLine = speciesLine.Substring(0, speciesLine.Length - 3).Trim();
                }
                else if (speciesLine.EndsWith("(F)", StringComparison.OrdinalIgnoreCase))
                {
                    gender = 2;
                    speciesLine = speciesLine.Substring(0, speciesLine.Length - 3).Trim();
                }
                pk.Gender = gender;

                string rawSpecies = speciesLine;
                int pOpen = speciesLine.LastIndexOf('(');
                int pClose = speciesLine.LastIndexOf(')');
                if (pOpen >= 0 && pClose > pOpen)
                {
                    rawSpecies = speciesLine.Substring(pOpen + 1, pClose - pOpen - 1).Trim();
                }

                ResolveSpeciesAndForm(rawSpecies, out ushort spIdx, out byte fIdx);
                pk.Species = spIdx;
                pk.Form = fIdx;

                if (!string.IsNullOrWhiteSpace(itemStr))
                {
                    int itmIdx = MatchItem(itemStr);
                    pk.Item = itmIdx >= 0 ? itmIdx : 0;
                }
                continue;
            }

            if (l.StartsWith("Ability:", StringComparison.OrdinalIgnoreCase))
            {
                abilityNameStr = l.Substring("Ability:".Length).Trim();
            }
            else if (l.StartsWith("Level:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(l.Substring("Level:".Length).Trim(), out int lv))
                {
                    pk.Level = (byte)Math.Max(1, Math.Min(100, lv));
                    levelSpecified = true;
                }
            }
            else if (l.StartsWith("Shiny:", StringComparison.OrdinalIgnoreCase))
            {
                string sVal = l.Substring("Shiny:".Length).Trim();
                pk.Shiny = sVal.Equals("Yes", StringComparison.OrdinalIgnoreCase) || sVal.Equals("True", StringComparison.OrdinalIgnoreCase);
            }
            else if (l.StartsWith("Happiness:", StringComparison.OrdinalIgnoreCase) || l.StartsWith("Friendship:", StringComparison.OrdinalIgnoreCase))
            {
                int colIdx = l.IndexOf(':');
                if (colIdx >= 0 && int.TryParse(l.Substring(colIdx + 1).Trim(), out int hap))
                    pk.Friendship = (byte)Math.Max(0, Math.Min(255, hap));
            }
            else if (l.StartsWith("EVs:", StringComparison.OrdinalIgnoreCase))
            {
                ParseStats(l.Substring("EVs:".Length).Trim(), ref hpev, ref atkev, ref defev, ref spaev, ref spdev, ref speev);
            }
            else if (l.StartsWith("IVs:", StringComparison.OrdinalIgnoreCase))
            {
                ParseStats(l.Substring("IVs:".Length).Trim(), ref hpiv, ref atkiv, ref defiv, ref spaiv, ref spdiv, ref speiv);
            }
            else if (l.EndsWith("Nature", StringComparison.OrdinalIgnoreCase))
            {
                string nStr = l.Replace("Nature", "").Trim();
                int natIdx = Array.FindIndex(natures, x => x != null && x.Equals(nStr, StringComparison.OrdinalIgnoreCase));
                if (natIdx >= 0) pk.Nature = natIdx;
            }
            else if (l.StartsWith("-") && moveIdx < 4)
            {
                string mName = l.Substring(1).Trim();
                int mvIdx = MatchMove(mName);
                if (mvIdx >= 0) moves[moveIdx++] = mvIdx;
            }
        }

        if (!levelSpecified)
        {
            pk.Level = 100;
        }

        if (!string.IsNullOrWhiteSpace(abilityNameStr))
            pk.Ability = ResolveAbilitySlot(pk.Species, pk.Form, abilityNameStr);
        else
            pk.Ability = 0;

        pk.IVs = new[] { hpiv, atkiv, defiv, spaiv, spdiv, speiv };
        pk.EVs = new[] { hpev, atkev, defev, spaev, spdev, speev };
        pk.Moves = moves;
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
    private void B_SaveSet_Click(object sender, EventArgs e) { if (currentSlot < 0 || currentSlot >= Trainers[index].Pokemon.Count) return; ShowdownSetManager.AddSet(GetExportString(Trainers[index].Pokemon[currentSlot]), "Set " + (CB_SetList.Items.Count + 1)); RefreshSetList(); }
    private void B_DeleteSet_Click(object sender, EventArgs e) { if (CB_SetList.SelectedIndex >= 0) { ShowdownSetManager.RemoveSet(CB_SetList.SelectedIndex); RefreshSetList(); } }
    private void CB_SetList_SelectedIndexChanged(object sender, EventArgs e) { if (CB_SetList.SelectedIndex < 0 || currentSlot < 0 || currentSlot >= Trainers[index].Pokemon.Count) return; ParseShowdownSet(ShowdownSetManager.Sets[CB_SetList.SelectedIndex].Content, Trainers[index].Pokemon[currentSlot]); PopulateFieldsTP7(Trainers[index].Pokemon[currentSlot]); GetQuickFiller(pba[currentSlot], Trainers[index].Pokemon[currentSlot]); }
    
    private string GetExportString(TrainerPoke7 pk)
    {
        StringBuilder sb = new StringBuilder();
        string name = (pk.Species < specieslist.Length) ? specieslist[pk.Species] : "Pikachu";
        
        if (pk.Form > 0 && AltForms != null && pk.Species < AltForms.Length && AltForms[pk.Species] != null)
        {
            string[] forms = AltForms[pk.Species];
            if (pk.Form < forms.Length && !string.IsNullOrWhiteSpace(forms[pk.Form]))
            {
                string fName = forms[pk.Form].Trim();
                name = $"{name}-{fName}";
            }
        }

        if (pk.Gender == 1) name += " (M)";
        else if (pk.Gender == 2) name += " (F)";

        string itemStr = (pk.Item > 0 && pk.Item < itemlist.Length) ? itemlist[pk.Item] : null;
        if (!string.IsNullOrWhiteSpace(itemStr) && itemStr != "(None)")
            sb.AppendLine($"{name} @ {itemStr}");
        else
            sb.AppendLine(name);

        int formIdx = Main.Config.Personal.GetFormIndex(pk.Species, pk.Form);
        if (formIdx < Main.SpeciesStat.Length)
        {
            var pStat = Main.SpeciesStat[formIdx];
            int abID = pk.Ability switch
            {
                1 => pStat.Abilities[0],
                2 => pStat.Abilities[1],
                3 => pStat.Abilities[2],
                _ => pStat.Abilities[0]
            };
            if (abID > 0 && abID < abilitylist.Length)
                sb.AppendLine($"Ability: {abilitylist[abID]}");
        }

        sb.AppendLine($"Level: {pk.Level}");

        if (pk.Shiny) sb.AppendLine("Shiny: Yes");

        if (pk.Friendship != 255 && pk.Friendship > 0)
            sb.AppendLine($"Happiness: {pk.Friendship}");

        List<string> evList = new List<string>();
        if (pk.EV_HP > 0) evList.Add($"{pk.EV_HP} HP");
        if (pk.EV_ATK > 0) evList.Add($"{pk.EV_ATK} Atk");
        if (pk.EV_DEF > 0) evList.Add($"{pk.EV_DEF} Def");
        if (pk.EV_SPA > 0) evList.Add($"{pk.EV_SPA} SpA");
        if (pk.EV_SPD > 0) evList.Add($"{pk.EV_SPD} SpD");
        if (pk.EV_SPE > 0) evList.Add($"{pk.EV_SPE} Spe");
        if (evList.Count > 0) sb.AppendLine("EVs: " + string.Join(" / ", evList));

        if (pk.Nature >= 0 && pk.Nature < natures.Length)
            sb.AppendLine($"{natures[pk.Nature]} Nature");

        List<string> ivList = new List<string>();
        if (pk.IV_HP != 31) ivList.Add($"{pk.IV_HP} HP");
        if (pk.IV_ATK != 31) ivList.Add($"{pk.IV_ATK} Atk");
        if (pk.IV_DEF != 31) ivList.Add($"{pk.IV_DEF} Def");
        if (pk.IV_SPA != 31) ivList.Add($"{pk.IV_SPA} SpA");
        if (pk.IV_SPD != 31) ivList.Add($"{pk.IV_SPD} SpD");
        if (pk.IV_SPE != 31) ivList.Add($"{pk.IV_SPE} Spe");
        if (ivList.Count > 0) sb.AppendLine("IVs: " + string.Join(" / ", ivList));

        foreach (int m in pk.Moves)
        {
            if (m > 0 && m < movelist.Length)
                sb.AppendLine($"- {movelist[m]}");
        }

        return sb.ToString().TrimEnd();
    }

    private void B_ExportTeam_Click(object sender, EventArgs e)
    {
        var tr = Trainers[index];
        if (tr.Pokemon == null || tr.Pokemon.Count == 0)
        {
            WinFormsUtil.Alert("Trainer has no Pokémon to export.");
            return;
        }
        StringBuilder sb = new StringBuilder();
        foreach (var p in tr.Pokemon)
        {
            sb.AppendLine(GetExportString(p)).AppendLine();
        }
        Clipboard.SetText(sb.ToString().TrimEnd());
        WinFormsUtil.Alert($"Exported team ({tr.Pokemon.Count} Pokémon) to Showdown format!");
    }

    private void B_ExportSet_Click(object sender, EventArgs e)
    {
        if (currentSlot < 0 || currentSlot >= Trainers[index].Pokemon.Count)
        {
            WinFormsUtil.Alert("Please select a valid Pokémon slot to copy.");
            return;
        }
        string setStr = GetExportString(Trainers[index].Pokemon[currentSlot]);
        Clipboard.SetText(setStr);
        WinFormsUtil.Alert($"Exported Showdown set for {specieslist[Trainers[index].Pokemon[currentSlot].Species]} to clipboard!");
    }

    private void B_MaxIVsAll_Click(object sender, EventArgs e) { foreach (var t in Trainers) foreach (var p in t.Pokemon) p.IV_HP = p.IV_ATK = p.IV_DEF = p.IV_SPA = p.IV_SPD = p.IV_SPE = 31; SaveAllEntries(); LoadEntry(); }
    private void B_DoublesAll_Click(object sender, EventArgs e) { foreach (var t in Trainers) t.Mode = BattleMode.Doubles; SaveAllEntries(); LoadEntry(); }
    private void B_PokeChangeAll_Click(object sender, EventArgs e) { foreach (var t in Trainers) t.AI |= (1 << 6); SaveAllEntries(); LoadEntry(); }
    private void B_Master_Click(object sender, EventArgs e) { CHK_AI0.Checked = CHK_AI1.Checked = CHK_AI2.Checked = true; }
    private void B_MasterAll_Click(object sender, EventArgs e) { foreach (var t in Trainers) t.AI |= 0x7; SaveAllEntries(); LoadEntry(); }

    private void B_Clear_Click(object sender, EventArgs e) => SetMoves(new int[4]);
    private void B_CurrentAttack_Click(object sender, EventArgs e) { var m = learn.GetCurrentMoves(CB_Species.SelectedIndex, CB_Forme.SelectedIndex, (int)NUD_Level.Value, 4); SetMoves(m); }
    private void B_HighAttack_Click(object sender, EventArgs e) { TB_ATKIV.Text = "31"; TB_ATKEV.Text = "252"; }
    private void SetMoves(IList<int> moves) { var cbs = new[] { CB_Move1, CB_Move2, CB_Move3, CB_Move4 }; for (int i = 0; i < 4; i++) cbs[i].SelectedIndex = moves[i]; }
    private void CB_Moves_SelectedIndexChanged(object sender, EventArgs e) { CHK_Damage.Enabled = CHK_STAB.Enabled = CB_Moves.SelectedIndex == 1; }
    private void CHK_Damage_CheckedChanged(object sender, EventArgs e) => NUD_Damage.Enabled = CHK_Damage.Checked;
    private void CHK_STAB_CheckedChanged(object sender, EventArgs e) => NUD_STAB.Enabled = CHK_STAB.Checked;
    private void CHK_RandomPKM_CheckedChanged(object sender, EventArgs e) { foreach (CheckBox c in new[] { CHK_G1, CHK_G2, CHK_G3, CHK_G4, CHK_G5, CHK_G6, CHK_G7, CHK_G8, CHK_G9 }) c.Enabled = CHK_RandomPKM.Checked; }
    private void CHK_RandomClass_CheckedChanged(object sender, EventArgs e) { CHK_IgnoreSpecialClass.Enabled = CHK_RandomClass.Checked; }
    private void CHK_RandomShiny_CheckedChanged(object sender, EventArgs e) => NUD_Shiny.Enabled = CHK_RandomShiny.Checked;
    private void CHK_Level_CheckedChanged(object sender, EventArgs e) => NUD_LevelBoost.Enabled = CHK_Level.Checked;
    private void SyncEVSlider(object sender, EventArgs e)
    {
        if (updatingStats || loading) return;
        var ts = new[] { TB_HPEV_Slider, TB_ATKEV_Slider, TB_DEFEV_Slider, TB_SPAEV_Slider, TB_SPDEV_Slider, TB_SPEEV_Slider };
        var tbs = new[] { TB_HPEV, TB_ATKEV, TB_DEFEV, TB_SPAEV, TB_SPDEV, TB_SPEEV };
        int idx = Array.IndexOf(ts, sender as TrackBar);
        if (idx < 0) return;

        TrackBar currentSlider = ts[idx];
        bool ignoreCap = CHK_IgnoreEVCap != null && CHK_IgnoreEVCap.Checked;

        if (!ignoreCap)
        {
            int otherEVs = 0;
            for (int k = 0; k < 6; k++)
            {
                if (k != idx) otherEVs += WinFormsUtil.ToInt32(tbs[k]);
            }
            int maxAllowed = Math.Max(0, Math.Min(252, 510 - otherEVs));
            if (currentSlider.Value > maxAllowed)
            {
                currentSlider.Value = maxAllowed;
            }
        }

        tbs[idx].Text = currentSlider.Value.ToString();
        UpdateStats(null, null);
    }

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
