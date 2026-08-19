using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core;
using pk3DS.Core.Modding.Research;
using pk3DS.Core.Randomizers;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms.Subforms;

public class UniversalRandomizerForm : Form
{
    private TabControl TC_Main;
    private TextBox TB_Seed;
    private Button B_NewSeed;
    private Button B_Randomize;
    private ProgressBar PB_Progress;
    private Label L_Status;

    // Top Panel Controls
    private CheckBox CHK_LimitPokemon, CHK_CompetitiveRandomizer, CHK_NoIrregularAltFormes, CHK_RaceMode;
    private Label L_ModeIndicator;
    private Label L_RomInfo;
    private Button B_LoadSettings, B_SaveSettings;

    // TAB 1: POKEMON TRAITS
    // Base Statistics
    private RadioButton RB_StatsUnchanged, RB_StatsShuffle, RB_StatsRandom;
    private CheckBox CHK_StatsFollowEvolutions, CHK_StatsFollowMegas, CHK_StatsRandomizeAddedStats, CHK_StatsUpdateToGen, CHK_StatsStandardizeExp;
    private ComboBox CB_StatsUpdateGen, CB_StatsStandardizeExpTarget;
    private RadioButton RB_ExpLegendariesSlow, RB_ExpStrongLegendariesSlow, RB_ExpAllPokemon;
    private TrackBar TB_StatVariance;
    private Label L_StatVarianceVal, L_StatVariance;

    // Types
    private RadioButton RB_TypesUnchanged, RB_TypesRandomEvos, RB_TypesRandomCompletely;
    private CheckBox CHK_TypesFollowMegas, CHK_TypesForceDual;
    private RadioButton RB_TypeEffUnchanged, RB_TypeEffRandom, RB_TypeEffBalanced, RB_TypeEffKeepIdentities, RB_TypeEffInverse;
    private CheckBox CHK_TypeEffAddImmunities;

    // Abilities
    private RadioButton RB_AbilUnchanged, RB_AbilRandom;
    private CheckBox CHK_AbilAllowWonderGuard, CHK_AbilFollowEvolutions, CHK_AbilFollowMegas, CHK_AbilTrapping, CHK_AbilCombineDuplicate, CHK_AbilNegative, CHK_AbilBad, CHK_AbilEnsureTwo;

    // Evolutions
    private RadioButton RB_EvosUnchanged, RB_EvosRandom, RB_EvosRandomEveryLevel;
    private CheckBox CHK_EvosSimilarStrength, CHK_EvosSameTyping, CHK_EvosLimitThreeStages, CHK_EvosForceChange, CHK_EvosAllowAltFormes, CHK_EvosChangeImpossible, CHK_EvosMakeEasier, CHK_EvosRemoveTimeBased;

    // TAB 2: STARTERS, STATICS & TRADES
    // Starters
    private RadioButton RB_StartersUnchanged, RB_StartersCustom, RB_StartersRandomCompletely, RB_StartersRandomBasic;
    private RadioButton RB_StarterTypeNone, RB_StarterTypeFWG, RB_StarterTypeTriangle, RB_StarterTypeUnique, RB_StarterTypeSingle;
    private CheckBox CHK_StarterNoDualTypes;
    private ComboBox CB_StarterSingleType;

    /// <summary>Type names in index order, for any tab that needs a type picker.</summary>
    private static readonly string[] TypeNames =
    [
        "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel",
        "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark", "Fairy",
    ];
    private ComboBox CB_Starter1, CB_Starter2, CB_Starter3;
    private CheckBox CHK_StarterHeldItems, CHK_StarterBanBadItems, CHK_StarterAllowAltFormes;

    // Statics
    private RadioButton RB_StaticsUnchanged, RB_StaticsSwap, RB_StaticsRandomCompletely, RB_StaticsRandomSimilar;
    private CheckBox CHK_Statics600BST, CHK_StaticsLimitMainGame, CHK_StaticsAllowAltFormes, CHK_StaticsSwapMega, CHK_StaticsFixMusic;
    private TrackBar TB_StaticsLevelMod;
    private Label L_StaticsLevelModVal;

    // Trades
    private RadioButton RB_TradesUnchanged, RB_TradesGivenOnly, RB_TradesBoth;
    private CheckBox CHK_TradesNicknames, CHK_TradesOTs, CHK_TradesIVs, CHK_TradesItems;

    // TAB 3: MOVES & MOVESETS
    // Move Data
    private CheckBox CHK_MovePower, CHK_MoveAccuracy, CHK_MovePP, CHK_MoveTypes, CHK_MoveCategory, CHK_MoveUpdateToGen;
    private ComboBox CB_MoveUpdateGen;

    // Movesets
    private RadioButton RB_MovesetsUnchanged, RB_MovesetsSameType, RB_MovesetsCompletely, RB_MovesetsMetronome;
    private CheckBox CHK_GuaranteedLv1Moves, CHK_ReorderDamagingMoves, CHK_NoGameBreakingMoves, CHK_ForcePercentGoodMoves, CHK_EvoMovesForAll;
    private TrackBar TB_GuaranteedLv1Count, TB_ForcePercentGoodMovesVal;
    private Label L_GuaranteedLv1Val, L_ForcePercentGoodVal;

    // TAB 4: FOE POKEMON
    // Trainer Pokemon
    private ComboBox CB_TrainerMode;
    private CheckBox CHK_TrainerAvoidDuplicates;
    private CheckBox CHK_DiverseBoss, CHK_DiverseImportant, CHK_DiverseRegular;
    private RadioButton RB_StyleUnchanged, RB_StyleRandom, RB_StyleSingle;
    private ComboBox CB_BattleStyle;
    private CheckBox CHK_TrainerDoubleBattle, CHK_TrainerBetterMovesets, CHK_RivalCarriesStarter, CHK_TrainerSimilarStrength, CHK_TrainerWeightTypes, CHK_TrainerNoLegendaries, CHK_TrainerNoEarlyWonderGuard, CHK_TrainerAllowAltFormes, CHK_TrainerSwapMega, CHK_TrainerRandomShiny, CHK_RandomizeTrainerNames, CHK_RandomizeTrainerClassNames, CHK_TrainerNoMegaEvolution, CHK_TrainerNoZMoves, CHK_TrainerFullIVs;
    private CheckBox CHK_AddBossPk, CHK_AddImportantPk, CHK_AddRegularPk;
    private NumericUpDown NUD_AddBossCount, NUD_AddImportantCount, NUD_AddRegularCount;
    private CheckBox CHK_ItemBossPk, CHK_ItemImportantPk, CHK_ItemRegularPk, CHK_ItemConsumableOnly, CHK_ItemSensibleOnly, CHK_ItemHighestLevelOnly;
    private CheckBox CHK_ForceFullyEvolved;
    private TrackBar TB_ForceFullyEvolvedLevel, TB_TrainerLevelMod;
    private Label L_ForceFullyEvolvedVal, L_TrainerLevelModVal;
    private CheckBox CHK_PokemonLeagueUnique;
    private NumericUpDown NUD_PokemonLeagueCount;

    // Totem Pokemon (Gen 7)
    private RadioButton RB_TotemUnchanged, RB_TotemRandom, RB_TotemRandomSimilar;
    private RadioButton RB_AllyUnchanged, RB_AllyRandom, RB_AllyRandomSimilar;
    private RadioButton RB_AuraUnchanged, RB_AuraRandom, RB_AuraRandomSame;
    private CheckBox CHK_TotemHeldItems, CHK_TotemAllowAltFormes;
    private TrackBar TB_TotemLevelMod;
    private Label L_TotemLevelModVal;

    // TAB 5: WILD POKEMON
    private RadioButton RB_WildUnchanged, RB_WildRandom, RB_WildArea1To1, RB_WildGlobal1To1;
    private RadioButton RB_WildRuleNone, RB_WildRuleSimilar, RB_WildRuleCatchEmAll, RB_WildRuleTypeThemed;
    private CheckBox CHK_WildTimeBased, CHK_WildNoLegendaries, CHK_WildMinCatchRate, CHK_WildHeldItems, CHK_WildBanBadItems, CHK_WildShakingGrass, CHK_WildAllowAltFormes;
    private ComboBox CB_WildReplacements;
    private CheckBox CHK_WildSplitByEncounterType, CHK_WildAvoidRepeats;
    private RadioButton RB_WildTypeNone, RB_WildTypeZoneThemes, RB_WildTypeKeepPrimary;
    private CheckBox CHK_WildKeepZoneThemes;
    private RadioButton RB_WildEvoNone, RB_WildEvoOnlyBasic, RB_WildEvoSameStage;
    private CheckBox CHK_WildKeepEvoRelations;
    private TrackBar TB_WildMinCatchRateVal, TB_WildLevelMod;
    private Label L_WildMinCatchRateVal, L_WildLevelModVal;

    // TAB 6: TM/HMS & TUTORS
    // TMs & HMs
    private RadioButton RB_TMMovesUnchanged, RB_TMMovesRandom, RB_TMMovesNoGameBreaking;
    private CheckBox CHK_KeepFieldTMs, CHK_TMForceGoodMoves;

    // Level Caps tab
    private CheckBox CHK_LevelCaps, CHK_CheapCandies;
    private ComboBox CB_LevelCapStyle;

    /// <summary>Difficulty entry that reads the curve off the ROM's trainers, past the offset list.</summary>
    private const string MatchTrainersLabel = "Match trainer levels";
    private NumericUpDown NUD_LevelCapFinal;
    private Label L_LevelCapSpace;
    private DataGridView DGV_LevelCaps;
    private TrackBar TB_TMForceGoodMovesVal;
    private Label L_TMForceGoodMovesVal;

    private RadioButton RB_TMCompUnchanged, RB_TMCompSameType, RB_TMCompCompletely, RB_TMCompFull;
    private CheckBox CHK_TMSanity, CHK_TMFollowEvos, CHK_FullHMComp;

    // Move Tutors
    private RadioButton RB_TutorMovesUnchanged, RB_TutorMovesRandom, RB_TutorMovesNoGameBreaking;
    private CheckBox CHK_KeepFieldTutors, CHK_TutorForceGoodMoves;
    private TrackBar TB_TutorForceGoodMovesVal;
    private Label L_TutorForceGoodMovesVal;

    private RadioButton RB_TutorCompUnchanged, RB_TutorCompSameType, RB_TutorCompCompletely, RB_TutorCompFull;
    private CheckBox CHK_TutorSanity, CHK_TutorFollowEvos;

    // TAB 7: ITEMS
    // Field Items
    private RadioButton RB_FieldItemsUnchanged, RB_FieldItemsShuffle, RB_FieldItemsRandom, RB_FieldItemsRandomEven;
    private CheckBox CHK_FieldItemsBanBad;

    // Special Shops
    private RadioButton RB_ShopsUnchanged, RB_ShopsShuffle, RB_ShopsRandom;
    private CheckBox CHK_ShopsBanBad, CHK_ShopsBanRegular, CHK_ShopsBanOverpowered, CHK_ShopsBalancePrices, CHK_GuaranteeEvolutionItems, CHK_GuaranteeXItems, CHK_ShopsRandomizeAll;

    // Pickup Items
    private RadioButton RB_PickupUnchanged, RB_PickupRandom;
    private CheckBox CHK_PickupBanBad;

    // TAB 8: MISC. TWEAKS
    private CheckBox CHK_BanLuckyEgg, CHK_BalanceStaticPokemonLevels, CHK_NoFreeLuckyEgg, CHK_DontRevertTempAltFormes, CHK_FastestText, CHK_AllWildPokemonCanCallAllies;
    private CheckBox CHK_FastEggHatching, CHK_NoEVsFromPokemon;
    private CheckBox CHK_ExpMultiplier;
    private NumericUpDown NUD_ExpMultiplier;
    private Label L_ExpMultiplier;

    private CheckedListBox CLB_Patches;
    private Label L_PatchHint;

    /// <summary>Recipe behind each row of <see cref="CLB_Patches"/>; null for a category heading.</summary>
    private readonly List<Recipe> _patchRows = [];

    // Limit Pokemon Settings State
    private bool _limitGen1 = true, _limitGen2 = true, _limitGen3 = true, _limitGen4 = true, _limitGen5 = true, _limitGen6 = true, _limitGen7 = true, _limitGen8 = true, _limitGen9 = true;
    private bool _limitAllowLegendaries = true, _limitAllowMythicals = true;

    // Minimum BST Floors Settings State
    private bool _enforceMinBST = true;
    private int _minBST3Stage1 = 300, _minBST3Stage2 = 450, _minBST3Stage3 = 600;
    private int _minBST2Stage1 = 350, _minBST2Stage2 = 550;
    private int _minBST1Stage = 500, _minBSTLegendary = 600;

    // Maximum BST Ceilings Settings State
    private bool _enforceMaxBST = false;
    private int _maxBST3Stage1 = 500, _maxBST3Stage2 = 600, _maxBST3Stage3 = 700;
    private int _maxBST2Stage1 = 550, _maxBST2Stage2 = 650;
    private int _maxBST1Stage = 600, _maxBSTLegendary = 780;

    // No Egregious Stats Settings State
    private bool _noEgregiousStats = false;
    private int _noEgregiousStatsSingleCap = 200;
    private int _noEgregiousStatsBSTCapRegular = 600, _noEgregiousStatsBSTCapLegendary = 780;

    // Avoid Minmaxing Settings State
    private bool _avoidMinmaxing = false;

    public UniversalRandomizerForm(bool competitiveMode = false)
    {
        InitializeComponent();
        WinFormsUtil.ApplyTheme(this);
        LoadSettingsToUI(new UniversalSettings());
        CHK_CompetitiveRandomizer.Checked = competitiveMode;
        L_ModeIndicator.Text = competitiveMode ? "Mode: Competitive" : "Mode: Regular";
    }

    private void InitializeComponent()
    {
        this.Text = "Universal Pokemon Randomizer ZX v4.6.1";

        this.Size = new Size(900, 780);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;

        // Top Controls Panel
        var PNL_TopHeader = new Panel { Location = new Point(10, 5), Size = new Size(865, 115), BackColor = Color.Transparent };

        var GB_GeneralOpts = new GroupBox { Text = "General Options", Location = new Point(5, 3), Size = new Size(175, 108) };
        CHK_LimitPokemon = new CheckBox { Text = "Limit Pokemon", Location = new Point(10, 18), AutoSize = true, Enabled = true };
        CHK_LimitPokemon.Click += (s, e) => {
            if (CHK_LimitPokemon.Checked)
                OpenLimitPokemonDialog();
        };
        CHK_CompetitiveRandomizer = new CheckBox { Text = "Competitive Mode", AutoSize = true };
        L_ModeIndicator = new Label { Text = "Mode: Regular", Location = new Point(10, 38), AutoSize = true, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
        CHK_NoIrregularAltFormes = new CheckBox { Text = "No Irregular Alt Formes", Location = new Point(10, 58), AutoSize = true };
        CHK_RaceMode = new CheckBox { Text = "Race Mode", Location = new Point(10, 78), AutoSize = true };
        GB_GeneralOpts.Controls.AddRange(new Control[] { CHK_LimitPokemon, L_ModeIndicator, CHK_NoIrregularAltFormes, CHK_RaceMode });

        var GB_RomInfo = new GroupBox { Text = "ROM Information", Location = new Point(190, 3), Size = new Size(240, 65) };
        L_RomInfo = new Label { Text = Main.RomFSPath != null ? $"ROM: {Main.Config.Version}" : "NO ROM LOADED", Location = new Point(10, 25), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        GB_RomInfo.Controls.Add(L_RomInfo);

        B_LoadSettings = new Button { Text = "Load Settings", Location = new Point(190, 76), Size = new Size(115, 30) };
        B_SaveSettings = new Button { Text = "Save Settings", Location = new Point(315, 76), Size = new Size(115, 30) };
        
        B_LoadSettings.Click += (s, e) => {
            string preset = Clipboard.GetText();
            if (preset.StartsWith("UPRZX3DS_"))
            {
                LoadSettingsToUI(UniversalSettings.ImportSettingsString(preset));
                WinFormsUtil.Alert("Settings loaded from clipboard!");
            }
            else
            {
                WinFormsUtil.Alert("Please copy a valid UPRZX3DS settings string to clipboard first!");
            }
        };
        B_SaveSettings.Click += (s, e) => {
            string str = GetSettingsFromUI().ExportSettingsString();
            Clipboard.SetText(str);
            WinFormsUtil.Alert("Settings string copied to clipboard!\n" + str);
        };

        B_Randomize = new Button { Text = "Randomize (Save)", Location = new Point(445, 8), Size = new Size(415, 50), BackColor = Color.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
        B_Randomize.Click += B_Randomize_Click;
        
        PNL_TopHeader.Controls.AddRange(new Control[] { GB_GeneralOpts, GB_RomInfo, B_LoadSettings, B_SaveSettings, B_Randomize });

        var L_VersionHeader = new Label { Text = "Based on Matt's Randomizer ZX Fork (Version 4.6.1)", Location = new Point(15, 125), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

        // Tab Control with 8 Tabs
        TC_Main = new TabControl { Location = new Point(10, 145), Size = new Size(865, 540) };

        BuildTabPokemonTraits();
        BuildTabStartersStaticsTrades();
        BuildTabMovesMovesets();
        BuildTabFoePokemon();
        BuildTabWildPokemon();
        BuildTabTMsHMsTutors();
        BuildTabItems();
        BuildTabLevelCaps();
        BuildTabMiscTweaks();

        foreach (TabPage page in TC_Main.TabPages)
            page.AutoScroll = true;

        // Bottom Seed & Progress Bar Panel
        var PNL_Bottom = new Panel { Location = new Point(10, 692), Size = new Size(865, 35), BackColor = Color.Transparent };
        var L_SeedLabel = new Label { Text = "Seed:", Location = new Point(5, 8), AutoSize = true };
        TB_Seed = new TextBox { Location = new Point(45, 5), Width = 160 };
        B_NewSeed = new Button { Text = "New Seed", Location = new Point(210, 4), Size = new Size(80, 24) };
        B_NewSeed.Click += (s, e) => { TB_Seed.Text = Util.Random32().ToString(); };

        L_Status = new Label { Text = "Ready.", Location = new Point(300, 8), AutoSize = true };
        PB_Progress = new ProgressBar { Location = new Point(450, 5), Size = new Size(410, 22) };

        PNL_Bottom.Controls.AddRange(new Control[] { L_SeedLabel, TB_Seed, B_NewSeed, L_Status, PB_Progress });

        this.Controls.AddRange(new Control[] { PNL_TopHeader, L_VersionHeader, TC_Main, PNL_Bottom });
    }

    private void BuildTabPokemonTraits()
    {
        var tab = new TabPage("Pokemon Traits");

        // Base Stats
        var GB_BaseStats = new GroupBox { Text = "Pokemon Base Statistics", Location = new Point(10, 10), Size = new Size(815, 130) };
        RB_StatsUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_StatsShuffle = new RadioButton { Text = "Shuffle", Location = new Point(15, 45), AutoSize = true };
        RB_StatsRandom = new RadioButton { Text = "Random", Location = new Point(15, 70), AutoSize = true };

        CHK_StatsFollowEvolutions = new CheckBox { Text = "Follow Evolutions", Location = new Point(130, 20), AutoSize = true };
        CHK_StatsFollowMegas = new CheckBox { Text = "Follow Mega Evolutions", Location = new Point(130, 45), AutoSize = true };
        CHK_StatsRandomizeAddedStats = new CheckBox { Text = "Randomize Added Stats on Evolution", Location = new Point(130, 70), AutoSize = true };

        CHK_StatsUpdateToGen = new CheckBox { Text = "Update Base Stats to Generation:", Location = new Point(340, 20), AutoSize = true };
        CB_StatsUpdateGen = new ComboBox { Location = new Point(558, 17), Width = 50, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_StatsUpdateGen.Items.AddRange(new object[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" });
        CB_StatsUpdateGen.SelectedIndex = 6;

        CHK_StatsStandardizeExp = new CheckBox { Text = "Standardize EXP Curves to:", Location = new Point(340, 50), AutoSize = true };
        CB_StatsStandardizeExpTarget = new ComboBox { Location = new Point(510, 47), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_StatsStandardizeExpTarget.Items.AddRange(new object[] { "Medium Fast", "Erratic", "Fast", "Medium Slow", "Slow", "Fluctuating" });
        CB_StatsStandardizeExpTarget.SelectedIndex = 0;

        RB_ExpLegendariesSlow = new RadioButton { Text = "Legendaries: Slow", Location = new Point(630, 18), AutoSize = true, Checked = true };
        RB_ExpStrongLegendariesSlow = new RadioButton { Text = "Strong Legendaries: Slow", Location = new Point(630, 42), AutoSize = true };
        RB_ExpAllPokemon = new RadioButton { Text = "All Pokemon", Location = new Point(630, 66), AutoSize = true };

        var B_MinBST = new Button { Text = "BST Floors / Ceilings...", Location = new Point(630, 92), Size = new Size(175, 28) };
        B_MinBST.Click += (s, e) => OpenBSTLimitsDialog();

        L_StatVariance = new Label { Text = "Stat Variance:", Location = new Point(340, 95), AutoSize = true };
        TB_StatVariance = new TrackBar { Location = new Point(425, 90), Width = 140, Minimum = 5, Maximum = 100, Value = 25, TickFrequency = 10, TickStyle = TickStyle.BottomRight, AutoSize = false, Height = 30 };
        L_StatVarianceVal = new Label { Text = "25%", Location = new Point(570, 95), AutoSize = true };
        TB_StatVariance.Scroll += (s, e) => L_StatVarianceVal.Text = $"{TB_StatVariance.Value}%";

        GB_BaseStats.Controls.AddRange(new Control[] { RB_StatsUnchanged, RB_StatsShuffle, RB_StatsRandom, CHK_StatsFollowEvolutions, CHK_StatsFollowMegas, CHK_StatsRandomizeAddedStats, CHK_StatsUpdateToGen, CB_StatsUpdateGen, CHK_StatsStandardizeExp, CB_StatsStandardizeExpTarget, RB_ExpLegendariesSlow, RB_ExpStrongLegendariesSlow, RB_ExpAllPokemon, B_MinBST, L_StatVariance, TB_StatVariance, L_StatVarianceVal });

        // Types
        var GB_Types = new GroupBox { Text = "Pokemon Types", Location = new Point(10, 140), Size = new Size(205, 122) };
        RB_TypesUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_TypesRandomEvos = new RadioButton { Text = "Random (follow evolutions)", Location = new Point(15, 40), AutoSize = true };
        RB_TypesRandomCompletely = new RadioButton { Text = "Random (completely)", Location = new Point(15, 60), AutoSize = true };
        CHK_TypesFollowMegas = new CheckBox { Text = "Follow Mega Evolutions", Location = new Point(15, 80), AutoSize = true };
        CHK_TypesForceDual = new CheckBox { Text = "Force Dual Types", Location = new Point(15, 100), AutoSize = true };
        GB_Types.Controls.AddRange(new Control[] { RB_TypesUnchanged, RB_TypesRandomEvos, RB_TypesRandomCompletely, CHK_TypesFollowMegas, CHK_TypesForceDual });

        // Abilities
        var GB_Abil = new GroupBox { Text = "Pokemon Abilities", Location = new Point(220, 140), Size = new Size(605, 122) };
        RB_AbilUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_AbilRandom = new RadioButton { Text = "Random", Location = new Point(15, 45), AutoSize = true };
        
        var B_BanAbil = new Button { Text = "Ban Abilities (Wonder Guard, Trapping, Negative)...", Location = new Point(15, 92), Size = new Size(360, 24) };
        B_BanAbil.Click += (s, e) => OpenBanAbilitiesDialog();

        CHK_AbilAllowWonderGuard = new CheckBox { Text = "Allow Wonder Guard", Location = new Point(140, 20), AutoSize = true };
        CHK_AbilFollowEvolutions = new CheckBox { Text = "Follow Evolutions", Location = new Point(140, 45), AutoSize = true, Checked = true };
        CHK_AbilTrapping = new CheckBox { Text = "Trapping Abilities", Location = new Point(140, 68), AutoSize = true };

        CHK_AbilCombineDuplicate = new CheckBox { Text = "Combine Duplicate Abilities", Location = new Point(280, 20), AutoSize = true };
        CHK_AbilFollowMegas = new CheckBox { Text = "Follow Mega Evolutions", Location = new Point(280, 45), AutoSize = true, Checked = true };
        CHK_AbilNegative = new CheckBox { Text = "Negative Abilities", Location = new Point(280, 68), AutoSize = true };

        CHK_AbilEnsureTwo = new CheckBox { Text = "Ensure Two Abilities", Location = new Point(460, 20), AutoSize = true };
        CHK_AbilBad = new CheckBox { Text = "Bad Abilities", Location = new Point(460, 68), AutoSize = true };

        GB_Abil.Controls.AddRange(new Control[] { RB_AbilUnchanged, RB_AbilRandom, B_BanAbil, CHK_AbilAllowWonderGuard, CHK_AbilFollowEvolutions, CHK_AbilTrapping, CHK_AbilCombineDuplicate, CHK_AbilFollowMegas, CHK_AbilNegative, CHK_AbilEnsureTwo, CHK_AbilBad });

        var GB_Evos = new GroupBox { Text = "Pokemon Evolutions", Location = new Point(10, 268), Size = new Size(815, 124) };
        RB_EvosUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_EvosRandom = new RadioButton { Text = "Random", Location = new Point(15, 50), AutoSize = true };
        RB_EvosRandomEveryLevel = new RadioButton { Text = "Random Every Level", Location = new Point(15, 80), AutoSize = true };

        CHK_EvosSimilarStrength = new CheckBox { Text = "Similar Strength", Location = new Point(160, 20), AutoSize = true, Checked = true };
        CHK_EvosSameTyping = new CheckBox { Text = "Same Typing", Location = new Point(160, 45), AutoSize = true };
        CHK_EvosLimitThreeStages = new CheckBox { Text = "Limit Evolutions to Three Stages", Location = new Point(160, 70), AutoSize = true, Checked = true };
        CHK_EvosForceChange = new CheckBox { Text = "Force Change", Location = new Point(160, 95), AutoSize = true };

        CHK_EvosAllowAltFormes = new CheckBox { Text = "Allow Alternate Formes", Location = new Point(360, 20), AutoSize = true, Checked = true };

        CHK_EvosChangeImpossible = new CheckBox { Text = "Change Impossible Evolutions", Location = new Point(560, 20), AutoSize = true, Checked = true };
        CHK_EvosMakeEasier = new CheckBox { Text = "Make Evolutions Easier", Location = new Point(560, 45), AutoSize = true };
        CHK_EvosRemoveTimeBased = new CheckBox { Text = "Remove Time-Based Evolutions", Location = new Point(560, 70), AutoSize = true };

        GB_Evos.Controls.AddRange(new Control[] { RB_EvosUnchanged, RB_EvosRandom, RB_EvosRandomEveryLevel, CHK_EvosSimilarStrength, CHK_EvosSameTyping, CHK_EvosLimitThreeStages, CHK_EvosForceChange, CHK_EvosAllowAltFormes, CHK_EvosChangeImpossible, CHK_EvosMakeEasier, CHK_EvosRemoveTimeBased });

        // Type Effectiveness - the battle chart in code.bin, not what type a Pokemon is (that is
        // the Pokemon Types group above). One row, because five radios and a checkbox is all it is.
        var GB_TypeEff = new GroupBox { Text = "Type Effectiveness", Location = new Point(10, 398), Size = new Size(815, 46) };
        RB_TypeEffUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(10, 17), AutoSize = true, Checked = true };
        RB_TypeEffRandom = new RadioButton { Text = "Random", Location = new Point(100, 17), AutoSize = true };
        RB_TypeEffBalanced = new RadioButton { Text = "Random (balanced)", Location = new Point(180, 17), AutoSize = true };
        RB_TypeEffKeepIdentities = new RadioButton { Text = "Keep Type Identities", Location = new Point(315, 17), AutoSize = true };
        RB_TypeEffInverse = new RadioButton { Text = "Inverse", Location = new Point(460, 17), AutoSize = true };
        CHK_TypeEffAddImmunities = new CheckBox { Text = "Add Random Immunities", Location = new Point(535, 17), AutoSize = true, Enabled = false };
        // Only Inverse can lose every immunity in the game, so only Inverse offers to put some back.
        RB_TypeEffInverse.CheckedChanged += (s, e) => CHK_TypeEffAddImmunities.Enabled = RB_TypeEffInverse.Checked;
        GB_TypeEff.Controls.AddRange(new Control[] { RB_TypeEffUnchanged, RB_TypeEffRandom, RB_TypeEffBalanced, RB_TypeEffKeepIdentities, RB_TypeEffInverse, CHK_TypeEffAddImmunities });

        tab.Controls.AddRange(new Control[] { GB_BaseStats, GB_Types, GB_Abil, GB_Evos, GB_TypeEff });
        TC_Main.TabPages.Add(tab);
    }

    private void BuildTabStartersStaticsTrades()
    {
        var tab = new TabPage("Starters, Statics & Trades");

        // Starters
        var GB_Starters = new GroupBox { Text = "Starter Pokemon", Location = new Point(10, 10), Size = new Size(815, 164) };  // 10..174
        RB_StartersUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_StartersCustom = new RadioButton { Text = "Custom", Location = new Point(15, 45), AutoSize = true };

        CB_Starter1 = new ComboBox { Location = new Point(90, 42), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_Starter2 = new ComboBox { Location = new Point(210, 42), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_Starter3 = new ComboBox { Location = new Point(330, 42), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };

        string[] monList = (Main.Config != null) 
            ? Main.Config.GetText(TextName.SpeciesNames).Skip(1).ToArray() 
            : Enumerable.Range(1, 1025).Select(i => i.ToString()).ToArray();

        CB_Starter1.Items.AddRange(monList); CB_Starter1.SelectedIndex = Math.Min(0, monList.Length - 1);
        CB_Starter2.Items.AddRange(monList); CB_Starter2.SelectedIndex = Math.Min(3, monList.Length - 1);
        CB_Starter3.Items.AddRange(monList); CB_Starter3.SelectedIndex = Math.Min(6, monList.Length - 1);

        RB_StartersRandomCompletely = new RadioButton { Text = "Random (completely)", Location = new Point(15, 68), AutoSize = true };
        RB_StartersRandomBasic = new RadioButton { Text = "Random (basic Pokemon with 2 evolutions)", Location = new Point(15, 89), AutoSize = true };

        CHK_StarterHeldItems = new CheckBox { Text = "Randomize Starter Held Items", Location = new Point(460, 20), AutoSize = true };
        CHK_StarterBanBadItems = new CheckBox { Text = "Ban Bad Items", Location = new Point(460, 45), AutoSize = true, Checked = true };
        CHK_StarterAllowAltFormes = new CheckBox { Text = "Allow Alternate Formes", Location = new Point(460, 70), AutoSize = true, Checked = true };

        var GB_StarterTypes = new GroupBox { Text = "Type Restrictions", Location = new Point(15, 112), Size = new Size(785, 46) };

        RB_StarterTypeNone = new RadioButton { Text = "None", Location = new Point(12, 18), AutoSize = true, Checked = true };
        RB_StarterTypeFWG = new RadioButton { Text = "Fire, Water, Grass", Location = new Point(75, 18), AutoSize = true };
        RB_StarterTypeTriangle = new RadioButton { Text = "Any Type Triangle", Location = new Point(200, 18), AutoSize = true };

        // "Unique" on its own says nothing about what is unique. The rule is that no two starters
        // share a type, so the label says that instead.
        RB_StarterTypeUnique = new RadioButton { Text = "No Shared Types", Location = new Point(325, 18), AutoSize = true };
        RB_StarterTypeSingle = new RadioButton { Text = "Single Type:", Location = new Point(450, 18), AutoSize = true };

        CB_StarterSingleType = new ComboBox { Location = new Point(538, 15), Width = 88, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_StarterSingleType.Items.AddRange([.. TypeNames]);
        CB_StarterSingleType.SelectedIndex = 0;

        CHK_StarterNoDualTypes = new CheckBox { Text = "No Dual Types", Location = new Point(645, 18), AutoSize = true };

        GB_StarterTypes.Controls.AddRange(new Control[]
        {
            RB_StarterTypeNone, RB_StarterTypeFWG, RB_StarterTypeTriangle, RB_StarterTypeUnique,
            RB_StarterTypeSingle, CB_StarterSingleType, CHK_StarterNoDualTypes,
        });

        GB_Starters.Controls.AddRange(new Control[]
        {
            RB_StartersUnchanged, RB_StartersCustom, CB_Starter1, CB_Starter2, CB_Starter3,
            RB_StartersRandomCompletely, RB_StartersRandomBasic,
            CHK_StarterHeldItems, CHK_StarterBanBadItems, CHK_StarterAllowAltFormes,
            GB_StarterTypes,
        });

        // Statics
        var GB_Statics = new GroupBox { Text = "Static Pokemon", Location = new Point(10, 179), Size = new Size(815, 140) };  // 179..319
        RB_StaticsUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_StaticsSwap = new RadioButton { Text = "Swap Legendaries & Swap Standards", Location = new Point(15, 45), AutoSize = true };
        RB_StaticsRandomCompletely = new RadioButton { Text = "Random (completely)", Location = new Point(15, 70), AutoSize = true };
        RB_StaticsRandomSimilar = new RadioButton { Text = "Random (similar strength)", Location = new Point(15, 95), AutoSize = true };

        CHK_Statics600BST = new CheckBox { Text = "Randomize 600+ BST", Location = new Point(260, 20), AutoSize = true };
        CHK_StaticsLimitMainGame = new CheckBox { Text = "Limit Main-Game Legendaries", Location = new Point(260, 45), AutoSize = true, Checked = true };
        CHK_StaticsAllowAltFormes = new CheckBox { Text = "Allow Alternate Formes", Location = new Point(260, 70), AutoSize = true, Checked = true };
        CHK_StaticsSwapMega = new CheckBox { Text = "Swap Mega Evolvables", Location = new Point(260, 95), AutoSize = true };
        CHK_StaticsFixMusic = new CheckBox { Text = "Fix Music", Location = new Point(260, 115), AutoSize = true };

        var L_StaticsLevel = new Label { Text = "Percentage Level Modifier:", Location = new Point(480, 20), AutoSize = true };
        TB_StaticsLevelMod = new TrackBar { Location = new Point(480, 40), Width = 300, Minimum = -100, Maximum = 100, Value = 0, SmallChange = 5, LargeChange = 10 };
        L_StaticsLevelModVal = new Label { Text = "0%", Location = new Point(620, 85), AutoSize = true };
        TB_StaticsLevelMod.Scroll += (s, e) => L_StaticsLevelModVal.Text = $"{TB_StaticsLevelMod.Value}%";

        GB_Statics.Controls.AddRange(new Control[] { RB_StaticsUnchanged, RB_StaticsSwap, RB_StaticsRandomCompletely, RB_StaticsRandomSimilar, CHK_Statics600BST, CHK_StaticsLimitMainGame, CHK_StaticsAllowAltFormes, CHK_StaticsSwapMega, CHK_StaticsFixMusic, L_StaticsLevel, TB_StaticsLevelMod, L_StaticsLevelModVal });

        // In-Game Trades
        var GB_Trades = new GroupBox { Text = "In-Game Trades", Location = new Point(10, 324), Size = new Size(815, 122) };  // 324..446
        RB_TradesUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_TradesGivenOnly = new RadioButton { Text = "Randomize Given Pokemon Only", Location = new Point(15, 45), AutoSize = true };
        RB_TradesBoth = new RadioButton { Text = "Randomize Both Requested & Given Pokemon", Location = new Point(15, 70), AutoSize = true };

        CHK_TradesNicknames = new CheckBox { Text = "Randomize Nicknames", Location = new Point(320, 20), AutoSize = true };
        CHK_TradesOTs = new CheckBox { Text = "Randomize OTs", Location = new Point(320, 45), AutoSize = true };
        CHK_TradesIVs = new CheckBox { Text = "Randomize IVs", Location = new Point(320, 70), AutoSize = true };
        CHK_TradesItems = new CheckBox { Text = "Randomize Items", Location = new Point(320, 95), AutoSize = true };

        GB_Trades.Controls.AddRange(new Control[] { RB_TradesUnchanged, RB_TradesGivenOnly, RB_TradesBoth, CHK_TradesNicknames, CHK_TradesOTs, CHK_TradesIVs, CHK_TradesItems });

        tab.Controls.AddRange(new Control[] { GB_Starters, GB_Statics, GB_Trades });
        TC_Main.TabPages.Add(tab);
    }

    private void BuildTabMovesMovesets()
    {
        var tab = new TabPage("Moves & Movesets");

        // Move Data
        var GB_MoveData = new GroupBox { Text = "Move Data", Location = new Point(10, 10), Size = new Size(815, 118) };
        CHK_MovePower = new CheckBox { Text = "Randomize Move Power", Location = new Point(15, 20), AutoSize = true };
        CHK_MoveAccuracy = new CheckBox { Text = "Randomize Move Accuracy", Location = new Point(15, 45), AutoSize = true };
        CHK_MovePP = new CheckBox { Text = "Randomize Move PP", Location = new Point(15, 70), AutoSize = true };
        CHK_MoveTypes = new CheckBox { Text = "Randomize Move Types", Location = new Point(15, 90), AutoSize = true };

        CHK_MoveCategory = new CheckBox { Text = "Randomize Move Category", Location = new Point(240, 20), AutoSize = true };

        CHK_MoveUpdateToGen = new CheckBox { Text = "Update Moves to Generation:", Location = new Point(240, 70), AutoSize = true };
        CB_MoveUpdateGen = new ComboBox { Location = new Point(438, 67), Width = 50, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_MoveUpdateGen.Items.AddRange(new object[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" });
        CB_MoveUpdateGen.SelectedIndex = 6;

        GB_MoveData.Controls.AddRange(new Control[] { CHK_MovePower, CHK_MoveAccuracy, CHK_MovePP, CHK_MoveTypes, CHK_MoveCategory, CHK_MoveUpdateToGen, CB_MoveUpdateGen });

        // Pokemon Movesets
        var GB_Movesets = new GroupBox { Text = "Pokemon Movesets", Location = new Point(10, 133), Size = new Size(825, 275) };
        RB_MovesetsUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_MovesetsSameType = new RadioButton { Text = "Random (preferring same type)", Location = new Point(15, 45), AutoSize = true };
        RB_MovesetsCompletely = new RadioButton { Text = "Random (completely)", Location = new Point(15, 70), AutoSize = true };
        RB_MovesetsMetronome = new RadioButton { Text = "Metronome Only Mode", Location = new Point(15, 95), AutoSize = true };

        CHK_GuaranteedLv1Moves = new CheckBox { Text = "Guaranteed Level 1 Moves", Location = new Point(410, 20), AutoSize = true };
        TB_GuaranteedLv1Count = new TrackBar { Location = new Point(580, 15), Width = 220, Minimum = 2, Maximum = 4, Value = 4 };
        L_GuaranteedLv1Val = new Label { Text = "4", Location = new Point(805, 20), AutoSize = true };
        TB_GuaranteedLv1Count.Scroll += (s, e) => L_GuaranteedLv1Val.Text = TB_GuaranteedLv1Count.Value.ToString();

        CHK_ReorderDamagingMoves = new CheckBox { Text = "Reorder Damaging Moves", Location = new Point(410, 55), AutoSize = true, Checked = true };
        CHK_NoGameBreakingMoves = new CheckBox { Text = "No Game-Breaking Moves", Location = new Point(410, 80), AutoSize = true, Checked = true };

        CHK_ForcePercentGoodMoves = new CheckBox { Text = "Force % of Good Damaging Moves:", Location = new Point(410, 105), AutoSize = true };
        TB_ForcePercentGoodMovesVal = new TrackBar { Location = new Point(410, 130), Width = 300, Minimum = 0, Maximum = 100, Value = 40, SmallChange = 5, LargeChange = 10 };
        L_ForcePercentGoodVal = new Label { Text = "40%", Location = new Point(715, 140), AutoSize = true };
        TB_ForcePercentGoodMovesVal.Scroll += (s, e) => L_ForcePercentGoodVal.Text = $"{TB_ForcePercentGoodMovesVal.Value}%";

        CHK_EvoMovesForAll = new CheckBox { Text = "Evolution Moves for All Pokemon", Location = new Point(410, 190), AutoSize = true };

        GB_Movesets.Controls.AddRange(new Control[] { RB_MovesetsUnchanged, RB_MovesetsSameType, RB_MovesetsCompletely, RB_MovesetsMetronome, CHK_GuaranteedLv1Moves, TB_GuaranteedLv1Count, L_GuaranteedLv1Val, CHK_ReorderDamagingMoves, CHK_NoGameBreakingMoves, CHK_ForcePercentGoodMoves, TB_ForcePercentGoodMovesVal, L_ForcePercentGoodVal, CHK_EvoMovesForAll });

        tab.Controls.AddRange(new Control[] { GB_MoveData, GB_Movesets });
        TC_Main.TabPages.Add(tab);
    }

    private void BuildTabFoePokemon()
    {
        var tab = new TabPage("Foe Pokemon");

        // Trainer Pokemon
        var GB_Trainers = new GroupBox { Text = "Trainer Pokemon", Location = new Point(10, 10), Size = new Size(815, 342) };

        // A dropdown rather than two radio buttons: there are seven modes now, and seven radios
        // would take the whole left column on their own.
        var L_TrainerMode = new Label { Text = "Trainer Pokemon:", Location = new Point(15, 20), AutoSize = true };
        CB_TrainerMode = new ComboBox { Location = new Point(15, 38), Width = 255, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_TrainerMode.Items.AddRange([.. TrainerThemes.Labels]);
        CB_TrainerMode.SelectedIndex = 0;

        CHK_TrainerBetterMovesets = new CheckBox { Text = "Better Movesets", Location = new Point(15, 66), AutoSize = true, Checked = true };
        CHK_TrainerAvoidDuplicates = new CheckBox { Text = "Try to Avoid Duplicates", Location = new Point(15, 88), AutoSize = true };

        CHK_TrainerDoubleBattle = new CheckBox { Text = "Double Battle Mode", AutoSize = true };

        // Additional Pokemon section - stacked vertically with 24px row spacing
        var L_AddPk = new Label { Text = "Additional Pokemon for...", Location = new Point(15, 114), AutoSize = true };
        CHK_AddBossPk = new CheckBox { Text = "Boss Trainers", Location = new Point(20, 134), AutoSize = true };
        NUD_AddBossCount = new NumericUpDown { Location = new Point(150, 133), Width = 40, Minimum = 1, Maximum = 6, Value = 1 };
        CHK_AddImportantPk = new CheckBox { Text = "Important Trainers", Location = new Point(20, 158), AutoSize = true };
        NUD_AddImportantCount = new NumericUpDown { Location = new Point(150, 157), Width = 40, Minimum = 1, Maximum = 6, Value = 1 };
        CHK_AddRegularPk = new CheckBox { Text = "Regular Trainers", Location = new Point(20, 182), AutoSize = true };
        NUD_AddRegularCount = new NumericUpDown { Location = new Point(150, 181), Width = 40, Minimum = 1, Maximum = 6, Value = 1 };

        // Held Items section - stacked vertically with 24px spacing
        var L_HeldItems = new Label { Text = "Add Held Items to...", Location = new Point(15, 208), AutoSize = true };
        CHK_ItemBossPk = new CheckBox { Text = "Boss Trainers", Location = new Point(20, 228), AutoSize = true };
        CHK_ItemImportantPk = new CheckBox { Text = "Important Trainers", Location = new Point(20, 252), AutoSize = true };
        CHK_ItemRegularPk = new CheckBox { Text = "Regular Trainers", Location = new Point(20, 276), AutoSize = true };
        CHK_ItemConsumableOnly = new CheckBox { Text = "Consumable Only", Location = new Point(170, 228), AutoSize = true };
        CHK_ItemSensibleOnly = new CheckBox { Text = "Sensible Items", Location = new Point(170, 252), AutoSize = true, Checked = true };
        CHK_ItemHighestLevelOnly = new CheckBox { Text = "Highest Level Only", Location = new Point(170, 276), AutoSize = true };

        // Middle column - trainer options
        CHK_RivalCarriesStarter = new CheckBox { Text = "Rival Carries Starter Through Game", Location = new Point(310, 18), AutoSize = true, Checked = true };
        CHK_TrainerSimilarStrength = new CheckBox { Text = "Try to Use Pokemon with Similar Strength", Location = new Point(310, 40), AutoSize = true, Checked = true };
        CHK_TrainerWeightTypes = new CheckBox { Text = "Weight Types by # of Pokemon", Location = new Point(310, 62), AutoSize = true };
        CHK_TrainerNoLegendaries = new CheckBox { Text = "Don't Use Legendaries", Location = new Point(310, 84), AutoSize = true, Checked = true };
        CHK_TrainerNoEarlyWonderGuard = new CheckBox { Text = "No Early Wonder Guard", Location = new Point(310, 106), AutoSize = true, Checked = true };
        CHK_TrainerAllowAltFormes = new CheckBox { Text = "Allow Alternate Formes", Location = new Point(310, 128), AutoSize = true, Checked = true };
        CHK_TrainerSwapMega = new CheckBox { Text = "Swap Mega Evolvables", Location = new Point(310, 150), AutoSize = true };
        CHK_TrainerRandomShiny = new CheckBox { Text = "Random Shiny Trainer Pokemon", Location = new Point(310, 172), AutoSize = true };

        var L_Diverse = new Label { Text = "Force Diverse Types for...", Location = new Point(310, 200), AutoSize = true };
        CHK_DiverseBoss = new CheckBox { Text = "Boss Trainers", Location = new Point(315, 220), AutoSize = true };
        CHK_DiverseImportant = new CheckBox { Text = "Important Trainers", Location = new Point(315, 242), AutoSize = true };
        CHK_DiverseRegular = new CheckBox { Text = "Regular Trainers", Location = new Point(315, 264), AutoSize = true };

        // Battle Style
        var GB_BattleStyle = new GroupBox { Text = "Battle Style", Location = new Point(310, 288), Size = new Size(495, 46) };
        RB_StyleUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(10, 17), AutoSize = true, Checked = true };
        RB_StyleRandom = new RadioButton { Text = "Random", Location = new Point(110, 17), AutoSize = true };
        RB_StyleSingle = new RadioButton { Text = "Single Style:", Location = new Point(200, 17), AutoSize = true };
        CB_BattleStyle = new ComboBox { Location = new Point(300, 15), Width = 175, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
        CB_BattleStyle.Items.AddRange(["Single Battles", "Double Battles"]);
        CB_BattleStyle.SelectedIndex = 0;
        RB_StyleSingle.CheckedChanged += (s, e) => CB_BattleStyle.Enabled = RB_StyleSingle.Checked;
        GB_BattleStyle.Controls.AddRange(new Control[] { RB_StyleUnchanged, RB_StyleRandom, RB_StyleSingle, CB_BattleStyle });

        // Right column - names, force evolved, level mod, league
        CHK_RandomizeTrainerNames = new CheckBox { Text = "Randomize Trainer Names", Location = new Point(580, 18), AutoSize = true };
        CHK_RandomizeTrainerClassNames = new CheckBox { Text = "Randomize Trainer Class Names", Location = new Point(580, 40), AutoSize = true };

        CHK_ForceFullyEvolved = new CheckBox { Text = "Force Fully Evolved at Level:", Location = new Point(580, 66), AutoSize = true };
        TB_ForceFullyEvolvedLevel = new TrackBar { Location = new Point(580, 86), Size = new Size(155, 35), Minimum = 30, Maximum = 65, Value = 36, TickStyle = TickStyle.BottomRight, AutoSize = false, TickFrequency = 5 };
        L_ForceFullyEvolvedVal = new Label { Text = "36", Location = new Point(740, 90), AutoSize = true };
        TB_ForceFullyEvolvedLevel.Scroll += (s, e) => L_ForceFullyEvolvedVal.Text = TB_ForceFullyEvolvedLevel.Value.ToString();

        var L_TrLevel = new Label { Text = "Percentage Level Modifier:", Location = new Point(580, 126), AutoSize = true };
        TB_TrainerLevelMod = new TrackBar { Location = new Point(580, 146), Size = new Size(155, 35), Minimum = -100, Maximum = 100, Value = 0, TickStyle = TickStyle.BottomRight, AutoSize = false, TickFrequency = 10 };
        L_TrainerLevelModVal = new Label { Text = "0%", Location = new Point(740, 150), AutoSize = true };
        TB_TrainerLevelMod.Scroll += (s, e) => L_TrainerLevelModVal.Text = $"{TB_TrainerLevelMod.Value}%";

        CHK_PokemonLeagueUnique = new CheckBox { Text = "League Unique Pokemon:", Location = new Point(580, 190), AutoSize = true };
        NUD_PokemonLeagueCount = new NumericUpDown { Location = new Point(760, 189), Width = 45, Minimum = 1, Maximum = 6, Value = 1 };

        CHK_TrainerNoMegaEvolution = new CheckBox { Text = "No Mega Evolution", Location = new Point(580, 218), AutoSize = true };
        CHK_TrainerNoZMoves = new CheckBox { Text = "No Z-Moves", Location = new Point(580, 240), AutoSize = true };
        CHK_TrainerFullIVs = new CheckBox { Text = "31 IVs (else random)", Location = new Point(580, 262), AutoSize = true, Checked = true };

        GB_Trainers.Controls.AddRange(new Control[] { L_TrainerMode, CB_TrainerMode, CHK_TrainerBetterMovesets, CHK_TrainerAvoidDuplicates, L_AddPk, CHK_AddBossPk, NUD_AddBossCount, CHK_AddImportantPk, NUD_AddImportantCount, CHK_AddRegularPk, NUD_AddRegularCount, L_HeldItems, CHK_ItemBossPk, CHK_ItemImportantPk, CHK_ItemRegularPk, CHK_ItemConsumableOnly, CHK_ItemSensibleOnly, CHK_ItemHighestLevelOnly, CHK_RivalCarriesStarter, CHK_TrainerSimilarStrength, CHK_TrainerWeightTypes, CHK_TrainerNoLegendaries, CHK_TrainerNoEarlyWonderGuard, CHK_TrainerAllowAltFormes, CHK_TrainerSwapMega, CHK_TrainerRandomShiny, L_Diverse, CHK_DiverseBoss, CHK_DiverseImportant, CHK_DiverseRegular, GB_BattleStyle, CHK_RandomizeTrainerNames, CHK_RandomizeTrainerClassNames, CHK_ForceFullyEvolved, TB_ForceFullyEvolvedLevel, L_ForceFullyEvolvedVal, L_TrLevel, TB_TrainerLevelMod, L_TrainerLevelModVal, CHK_PokemonLeagueUnique, NUD_PokemonLeagueCount, CHK_TrainerNoMegaEvolution, CHK_TrainerNoZMoves, CHK_TrainerFullIVs });

        // Totem Pokemon
        var GB_Totems = new GroupBox { Text = "Totem Pokemon (Gen 7)", Location = new Point(10, 358), Size = new Size(815, 136) };
        RB_TotemUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_TotemRandom = new RadioButton { Text = "Random", Location = new Point(15, 45), AutoSize = true };
        RB_TotemRandomSimilar = new RadioButton { Text = "Random (similar strength)", Location = new Point(15, 70), AutoSize = true };

        var GB_Ally = new GroupBox { Text = "Ally Pokemon", Location = new Point(190, 15), Size = new Size(175, 100) };
        RB_AllyUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(10, 20), AutoSize = true, Checked = true };
        RB_AllyRandom = new RadioButton { Text = "Random", Location = new Point(10, 45), AutoSize = true };
        RB_AllyRandomSimilar = new RadioButton { Text = "Random (similar strength)", Location = new Point(10, 70), AutoSize = true };
        GB_Ally.Controls.AddRange(new Control[] { RB_AllyUnchanged, RB_AllyRandom, RB_AllyRandomSimilar });

        var GB_Aura = new GroupBox { Text = "Auras", Location = new Point(375, 15), Size = new Size(165, 100) };
        RB_AuraUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(10, 20), AutoSize = true, Checked = true };
        RB_AuraRandom = new RadioButton { Text = "Random", Location = new Point(10, 45), AutoSize = true };
        RB_AuraRandomSame = new RadioButton { Text = "Random (same strength)", Location = new Point(10, 70), AutoSize = true };
        GB_Aura.Controls.AddRange(new Control[] { RB_AuraUnchanged, RB_AuraRandom, RB_AuraRandomSame });

        CHK_TotemHeldItems = new CheckBox { Text = "Randomize Held Items", Location = new Point(550, 20), AutoSize = true };
        CHK_TotemAllowAltFormes = new CheckBox { Text = "Allow Alternate Formes", Location = new Point(550, 45), AutoSize = true, Checked = true };

        var L_TotemLevel = new Label { Text = "Percentage Level Modifier:", Location = new Point(550, 70), AutoSize = true };
        TB_TotemLevelMod = new TrackBar { Location = new Point(550, 85), Width = 210, Minimum = -100, Maximum = 100, Value = 0 };
        L_TotemLevelModVal = new Label { Text = "0%", Location = new Point(765, 90), AutoSize = true };
        TB_TotemLevelMod.Scroll += (s, e) => L_TotemLevelModVal.Text = $"{TB_TotemLevelMod.Value}%";

        GB_Totems.Controls.AddRange(new Control[] { RB_TotemUnchanged, RB_TotemRandom, RB_TotemRandomSimilar, GB_Ally, GB_Aura, CHK_TotemHeldItems, CHK_TotemAllowAltFormes, L_TotemLevel, TB_TotemLevelMod, L_TotemLevelModVal });

        tab.Controls.AddRange(new Control[] { GB_Trainers, GB_Totems });
        TC_Main.TabPages.Add(tab);
    }

    private void BuildTabWildPokemon()
    {
        var tab = new TabPage("Wild Pokemon");

        var GB_Wild = new GroupBox { Text = "Wild Pokemon", Location = new Point(10, 10), Size = new Size(815, 418) };
        RB_WildUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 30), AutoSize = true, Checked = true };
        RB_WildRandom = new RadioButton { Text = "Random", Location = new Point(15, 60), AutoSize = true };
        RB_WildArea1To1 = new RadioButton { Text = "Area 1-to-1 Mapping", Location = new Point(15, 90), AutoSize = true };
        RB_WildGlobal1To1 = new RadioButton { Text = "Global 1-to-1 Mapping", Location = new Point(15, 120), AutoSize = true };

        var GB_Rule = new GroupBox { Text = "Additional Rule", Location = new Point(175, 25), Size = new Size(150, 140) };
        RB_WildRuleNone = new RadioButton { Text = "None", Location = new Point(10, 20), AutoSize = true, Checked = true };
        RB_WildRuleSimilar = new RadioButton { Text = "Similar Strength", Location = new Point(10, 50), AutoSize = true };
        RB_WildRuleCatchEmAll = new RadioButton { Text = "Catch Em All Mode", Location = new Point(10, 80), AutoSize = true };
        RB_WildRuleTypeThemed = new RadioButton { Text = "Type Themed Areas", Location = new Point(10, 110), AutoSize = true };
        GB_Rule.Controls.AddRange(new Control[] { RB_WildRuleNone, RB_WildRuleSimilar, RB_WildRuleCatchEmAll, RB_WildRuleTypeThemed });

        CHK_WildTimeBased = new CheckBox { Text = "Use Time Based Encounters", Location = new Point(380, 25), AutoSize = true };
        CHK_WildNoLegendaries = new CheckBox { Text = "Don't Use Legendaries", Location = new Point(380, 55), AutoSize = true, Checked = true };

        CHK_WildMinCatchRate = new CheckBox { Text = "Set Minimum Catch Rate:", Location = new Point(380, 85), AutoSize = true };
        TB_WildMinCatchRateVal = new TrackBar { Location = new Point(550, 80), Width = 220, Minimum = 1, Maximum = 5, Value = 3 };
        L_WildMinCatchRateVal = new Label { Text = "3", Location = new Point(775, 85), AutoSize = true };
        TB_WildMinCatchRateVal.Scroll += (s, e) => L_WildMinCatchRateVal.Text = TB_WildMinCatchRateVal.Value.ToString();

        CHK_WildHeldItems = new CheckBox { Text = "Randomize Held Items", Location = new Point(380, 125), AutoSize = true };
        CHK_WildBanBadItems = new CheckBox { Text = "Ban Bad Items", Location = new Point(380, 155), AutoSize = true, Checked = true };
        CHK_WildShakingGrass = new CheckBox { Text = "Balance Shaking Grass Pokemon", Location = new Point(380, 185), AutoSize = true };

        var L_WildLevel = new Label { Text = "Percentage Level Modifier:", Location = new Point(380, 215), AutoSize = true };
        TB_WildLevelMod = new TrackBar { Location = new Point(380, 233), Width = 220, Minimum = -100, Maximum = 100, Value = 0 };
        L_WildLevelModVal = new Label { Text = "0%", Location = new Point(605, 240), AutoSize = true };
        TB_WildLevelMod.Scroll += (s, e) => L_WildLevelModVal.Text = $"{TB_WildLevelMod.Value}%";

        CHK_WildAllowAltFormes = new CheckBox { Text = "Allow Alternate Formes", Location = new Point(640, 185), AutoSize = true, Checked = true };

        // Replacements Per Species - how widely one original species may be replaced by different
        // things. A dropdown because the five values are a scale, not five unrelated choices.
        var GB_WildReplacements = new GroupBox { Text = "Replacements Per Species", Location = new Point(615, 285), Size = new Size(190, 120) };
        CB_WildReplacements = new ComboBox { Location = new Point(10, 25), Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_WildReplacements.Items.AddRange(
        [
            "Maximum Possible",
            "1 Per Encounter Set",
            "1 Per Map",
            "1 Per Named Location",
            "1 In Whole Game",
        ]);
        CB_WildReplacements.SelectedIndex = 0;
        CHK_WildSplitByEncounterType = new CheckBox { Text = "Split by encounter types", Location = new Point(12, 58), AutoSize = true, Enabled = false };
        // Only 1 Per Map has anything to split: the wider scopes deliberately pool encounter types
        // together, and the narrower ones are already split finer than this would make them.
        CB_WildReplacements.SelectedIndexChanged += (s, e) =>
            CHK_WildSplitByEncounterType.Enabled = CB_WildReplacements.SelectedIndex == Wild7Randomizer.ScopePerMap;
        CHK_WildAvoidRepeats = new CheckBox { Text = "Avoid repeats in a table", Location = new Point(12, 86), AutoSize = true };
        GB_WildReplacements.Controls.AddRange(new Control[] { CB_WildReplacements, CHK_WildSplitByEncounterType, CHK_WildAvoidRepeats });

        var GB_WildTypes = new GroupBox { Text = "Type Restrictions", Location = new Point(15, 285), Size = new Size(285, 120) };
        RB_WildTypeNone = new RadioButton { Text = "None", Location = new Point(10, 20), AutoSize = true, Checked = true };
        RB_WildTypeZoneThemes = new RadioButton { Text = "Random Zone Themes", Location = new Point(10, 44), AutoSize = true };
        RB_WildTypeKeepPrimary = new RadioButton { Text = "Keep Primary Type", Location = new Point(10, 68), AutoSize = true };
        CHK_WildKeepZoneThemes = new CheckBox { Text = "Keep Set/Zone Themes", Location = new Point(28, 92), AutoSize = true, Enabled = false };
        RB_WildTypeZoneThemes.CheckedChanged += (s, e) => CHK_WildKeepZoneThemes.Enabled = RB_WildTypeZoneThemes.Checked;
        GB_WildTypes.Controls.AddRange(new Control[] { RB_WildTypeNone, RB_WildTypeZoneThemes, RB_WildTypeKeepPrimary, CHK_WildKeepZoneThemes });

        var GB_WildEvos = new GroupBox { Text = "Evolution Restrictions", Location = new Point(315, 285), Size = new Size(285, 120) };
        RB_WildEvoNone = new RadioButton { Text = "None", Location = new Point(10, 20), AutoSize = true, Checked = true };
        RB_WildEvoOnlyBasic = new RadioButton { Text = "Only Basic Pokemon", Location = new Point(10, 44), AutoSize = true };
        RB_WildEvoSameStage = new RadioButton { Text = "Same Evolution Stage", Location = new Point(10, 68), AutoSize = true };
        CHK_WildKeepEvoRelations = new CheckBox { Text = "Keep Evolution Relations", Location = new Point(28, 92), AutoSize = true };
        GB_WildEvos.Controls.AddRange(new Control[] { RB_WildEvoNone, RB_WildEvoOnlyBasic, RB_WildEvoSameStage, CHK_WildKeepEvoRelations });

        GB_Wild.Controls.AddRange(new Control[] { RB_WildUnchanged, RB_WildRandom, RB_WildArea1To1, RB_WildGlobal1To1, GB_Rule, CHK_WildTimeBased, CHK_WildNoLegendaries, CHK_WildMinCatchRate, TB_WildMinCatchRateVal, L_WildMinCatchRateVal, CHK_WildHeldItems, CHK_WildBanBadItems, CHK_WildShakingGrass, L_WildLevel, TB_WildLevelMod, L_WildLevelModVal, CHK_WildAllowAltFormes, GB_WildReplacements, GB_WildTypes, GB_WildEvos });

        tab.Controls.Add(GB_Wild);
        TC_Main.TabPages.Add(tab);
    }

    private void BuildTabTMsHMsTutors()
    {
        var tab = new TabPage("TM/HMs & Tutors");

        // TMs & HMs
        var GB_TMHM = new GroupBox { Text = "TMs & HMs", Location = new Point(10, 10), Size = new Size(815, 190) };
        var GB_TMMoves = new GroupBox { Text = "TM/HM Moves", Location = new Point(15, 20), Size = new Size(385, 155) };
        RB_TMMovesUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_TMMovesRandom = new RadioButton { Text = "Random", Location = new Point(15, 50), AutoSize = true };
        RB_TMMovesNoGameBreaking = new RadioButton { Text = "No Game-Breaking Moves", Location = new Point(15, 80), AutoSize = true };

        CHK_KeepFieldTMs = new CheckBox { Text = "Keep Field Move TMs", Location = new Point(180, 20), AutoSize = true, Checked = true };
        CHK_TMForceGoodMoves = new CheckBox { Text = "Force % of Good Damaging Moves:", Location = new Point(180, 50), AutoSize = true };
        TB_TMForceGoodMovesVal = new TrackBar { Location = new Point(180, 75), Width = 190, Minimum = 0, Maximum = 100, Value = 40 };
        L_TMForceGoodMovesVal = new Label { Text = "40%", Location = new Point(320, 120), AutoSize = true };
        TB_TMForceGoodMovesVal.Scroll += (s, e) => L_TMForceGoodMovesVal.Text = $"{TB_TMForceGoodMovesVal.Value}%";
        GB_TMMoves.Controls.AddRange(new Control[] { RB_TMMovesUnchanged, RB_TMMovesRandom, RB_TMMovesNoGameBreaking, CHK_KeepFieldTMs, CHK_TMForceGoodMoves, TB_TMForceGoodMovesVal, L_TMForceGoodMovesVal });

        var GB_TMComp = new GroupBox { Text = "TM/HM Compatibility", Location = new Point(415, 20), Size = new Size(385, 155) };
        RB_TMCompUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_TMCompSameType = new RadioButton { Text = "Random (prefer same type)", Location = new Point(15, 45), AutoSize = true };
        RB_TMCompCompletely = new RadioButton { Text = "Random (completely)", Location = new Point(15, 70), AutoSize = true };
        RB_TMCompFull = new RadioButton { Text = "Full Compatibility", Location = new Point(15, 95), AutoSize = true };

        CHK_TMSanity = new CheckBox { Text = "TM/Levelup Move Sanity", Location = new Point(200, 20), AutoSize = true, Checked = true };
        CHK_TMFollowEvos = new CheckBox { Text = "Follow Evolutions", Location = new Point(200, 45), AutoSize = true, Checked = true };
        CHK_FullHMComp = new CheckBox { Text = "Full HM Compatibility", Location = new Point(200, 70), AutoSize = true };
        GB_TMComp.Controls.AddRange(new Control[] { RB_TMCompUnchanged, RB_TMCompSameType, RB_TMCompCompletely, RB_TMCompFull, CHK_TMSanity, CHK_TMFollowEvos, CHK_FullHMComp });

        GB_TMHM.Controls.AddRange(new Control[] { GB_TMMoves, GB_TMComp });

        // Move Tutors
        var GB_Tutors = new GroupBox { Text = "Move Tutors", Location = new Point(10, 205), Size = new Size(815, 190) };
        var GB_TutorMoves = new GroupBox { Text = "Move Tutor Moves", Location = new Point(15, 20), Size = new Size(385, 155) };
        RB_TutorMovesUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_TutorMovesRandom = new RadioButton { Text = "Random", Location = new Point(15, 50), AutoSize = true };
        RB_TutorMovesNoGameBreaking = new RadioButton { Text = "No Game-Breaking Moves", Location = new Point(15, 80), AutoSize = true };

        CHK_KeepFieldTutors = new CheckBox { Text = "Keep Field Move Tutors", Location = new Point(180, 20), AutoSize = true, Checked = true };
        CHK_TutorForceGoodMoves = new CheckBox { Text = "Force % of Good Damaging Moves:", Location = new Point(180, 50), AutoSize = true };
        TB_TutorForceGoodMovesVal = new TrackBar { Location = new Point(180, 75), Width = 190, Minimum = 0, Maximum = 100, Value = 40 };
        L_TutorForceGoodMovesVal = new Label { Text = "40%", Location = new Point(320, 120), AutoSize = true };
        TB_TutorForceGoodMovesVal.Scroll += (s, e) => L_TutorForceGoodMovesVal.Text = $"{TB_TutorForceGoodMovesVal.Value}%";
        GB_TutorMoves.Controls.AddRange(new Control[] { RB_TutorMovesUnchanged, RB_TutorMovesRandom, RB_TutorMovesNoGameBreaking, CHK_KeepFieldTutors, CHK_TutorForceGoodMoves, TB_TutorForceGoodMovesVal, L_TutorForceGoodMovesVal });

        var GB_TutorComp = new GroupBox { Text = "Move Tutor Compatibility", Location = new Point(415, 20), Size = new Size(385, 155) };
        RB_TutorCompUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_TutorCompSameType = new RadioButton { Text = "Random (prefer same type)", Location = new Point(15, 45), AutoSize = true };
        RB_TutorCompCompletely = new RadioButton { Text = "Random (completely)", Location = new Point(15, 70), AutoSize = true };
        RB_TutorCompFull = new RadioButton { Text = "Full Compatibility", Location = new Point(15, 95), AutoSize = true };

        CHK_TutorSanity = new CheckBox { Text = "Tutor/Levelup Move Sanity", Location = new Point(200, 20), AutoSize = true, Checked = true };
        CHK_TutorFollowEvos = new CheckBox { Text = "Follow Evolutions", Location = new Point(200, 45), AutoSize = true, Checked = true };
        GB_TutorComp.Controls.AddRange(new Control[] { RB_TutorCompUnchanged, RB_TutorCompSameType, RB_TutorCompCompletely, RB_TutorCompFull, CHK_TutorSanity, CHK_TutorFollowEvos });

        GB_Tutors.Controls.AddRange(new Control[] { GB_TutorMoves, GB_TutorComp });

        tab.Controls.AddRange(new Control[] { GB_TMHM, GB_Tutors });
        TC_Main.TabPages.Add(tab);
    }

    /// <summary>
    /// Every checkpoint, editable, with the presets as a way to fill them quickly.
    /// </summary>
    private void BuildTabLevelCaps()
    {
        var tab = new TabPage("Level Caps");

        var GB = new GroupBox { Text = "Story Level Caps", Location = new Point(10, 10), Size = new Size(815, 400) };

        CHK_LevelCaps = new CheckBox
        {
            Text = "Cap Pokémon levels to story progress",
            Location = new Point(15, 22),
            AutoSize = true,
        };

        var L_Style = new Label { Text = "Difficulty:", Location = new Point(35, 54), AutoSize = true };
        CB_LevelCapStyle = new ComboBox { Location = new Point(105, 50), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
        CB_LevelCapStyle.Items.AddRange([.. LevelCapShifts.All.Select(s => (object)LevelCapShifts.Describe(s))]);
        // One past the offsets: this one does not shift the researched curve, it replaces the source
        // of the curve, so it cannot be expressed as a number of levels.
        CB_LevelCapStyle.Items.Add(MatchTrainersLabel);
        CB_LevelCapStyle.SelectedIndex = LevelCapShifts.StandardIndex;

        var L_Final = new Label { Text = "Final cap:", Location = new Point(330, 54), AutoSize = true };
        NUD_LevelCapFinal = new NumericUpDown
        {
            Location = new Point(402, 50),
            Width = 60,
            Minimum = 5,
            Maximum = LevelCapTable.HardCeiling,
            Value = LevelCapTable.HardCeiling,
        };

        var B_ApplyPreset = new Button { Text = "Fill Caps", Location = new Point(478, 49), Width = 90 };
        var B_ResetCaps = new Button { Text = "Reset to Research", Location = new Point(578, 49), Width = 130 };

        DGV_LevelCaps = new DataGridView
        {
            Location = new Point(15, 84),
            Size = new Size(785, 245),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        };
        DGV_LevelCaps.Columns.Add(new DataGridViewTextBoxColumn { Name = "Checkpoint", HeaderText = "Checkpoint", Width = 470, ReadOnly = true });
        DGV_LevelCaps.Columns.Add(new DataGridViewTextBoxColumn { Name = "Flag", HeaderText = "Flag", Width = 90, ReadOnly = true });
        DGV_LevelCaps.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cap", HeaderText = "Level Cap", Width = 100 });

        L_LevelCapSpace = new Label { Location = new Point(15, 338), AutoSize = true, MaximumSize = new Size(780, 0), ForeColor = Color.DimGray };

        CHK_CheapCandies = new CheckBox
        {
            Text = "Add Cheap Rare Candies  (10 PokéDollars, stocked in every mart regardless of Trials)",
            Location = new Point(15, 368),
            AutoSize = true,
        };

        void SetEnabled()
        {
            bool on = CHK_LevelCaps.Checked;
            CB_LevelCapStyle.Enabled = NUD_LevelCapFinal.Enabled = on;
            B_ApplyPreset.Enabled = B_ResetCaps.Enabled = DGV_LevelCaps.Enabled = on;
            L_LevelCapSpace.Text = DescribeLevelCapSpace();
        }

        void FillFromPreset()
        {
            int pick = Math.Max(0, CB_LevelCapStyle.SelectedIndex);

            if (pick >= LevelCapShifts.All.Length)
            {
                var levels = TrainerLevelSampler.Collect(Main.Config);
                LoadCapGrid(LevelCapPresets.BuildFromTrainerLevels(
                    levels, (byte)NUD_LevelCapFinal.Value, IsUltraMoonLoaded));
                L_LevelCapSpace.Text = levels.Count >= 20
                    ? $"Matched to {levels.Count} trainers (levels {levels[0]}-{levels[^1]}). " + DescribeLevelCapSpace()
                    : "Not enough trainer data to match; showing the research values. " + DescribeLevelCapSpace();
                return;
            }

            LoadCapGrid(LevelCapPresets.Build(
                LevelCapShifts.All[pick], (byte)NUD_LevelCapFinal.Value, IsUltraMoonLoaded));
            L_LevelCapSpace.Text = DescribeLevelCapSpace();
        }

        CHK_LevelCaps.CheckedChanged += (s, e) => SetEnabled();

        // Changing either control refills the grid immediately. Previously they only took effect on
        // the button, so picking a difficulty appeared to do nothing at all.
        CB_LevelCapStyle.SelectedIndexChanged += (s, e) => FillFromPreset();
        NUD_LevelCapFinal.ValueChanged += (s, e) => FillFromPreset();

        B_ApplyPreset.Click += (s, e) => FillFromPreset();
        B_ResetCaps.Click += (s, e) => LoadCapGrid(LevelCapTable.Default(IsUltraMoonLoaded));

        // Caps are levels; anything else is rejected as it is typed rather than at install time.
        DGV_LevelCaps.CellValidating += (s, e) =>
        {
            if (e.ColumnIndex != 2) return;
            if (!int.TryParse(Convert.ToString(e.FormattedValue), out int v) || v < 1 || v > LevelCapTable.HardCeiling)
                e.Cancel = true;
        };

        GB.Controls.AddRange(new Control[]
        {
            CHK_LevelCaps, L_Style, CB_LevelCapStyle, L_Final, NUD_LevelCapFinal,
            B_ApplyPreset, B_ResetCaps, DGV_LevelCaps, L_LevelCapSpace, CHK_CheapCandies,
        });
        tab.Controls.Add(GB);
        TC_Main.TabPages.Add(tab);

        LoadCapGrid(LevelCapTable.Default(IsUltraMoonLoaded));
        SetEnabled();
    }

    /// <summary>Whether the loaded game is Ultra Moon, for version-specific place names.</summary>
    /// <summary>
    /// Whether the loaded game is Ultra Moon, for the Sunne/Moone place names on the cap grid.
    /// </summary>
    private static bool IsUltraMoonLoaded =>
        string.Equals(ResearchVersion.Resolve(Main.Config), "UM", StringComparison.OrdinalIgnoreCase);

    /// <summary>Shows a table in the grid, one row per checkpoint.</summary>
    private void LoadCapGrid(LevelCapTable table)
    {
        DGV_LevelCaps.Rows.Clear();
        foreach (var e in table.Entries)
            DGV_LevelCaps.Rows.Add(e.Label, $"{e.FlagOffset:X2}/{e.FlagBit:X2}", (int)e.Cap);
    }

    /// <summary>
    /// Puts previously saved caps back into the grid.
    /// </summary>
    private void ApplySavedCaps(List<int> caps)
    {
        if (caps is not { Count: > 0 } || caps.Count != DGV_LevelCaps.Rows.Count) return;

        for (int i = 0; i < caps.Count; i++)
            DGV_LevelCaps.Rows[i].Cells[2].Value = caps[i];
    }

    /// <summary>The caps currently in the grid, in order.</summary>
    private List<int> ReadCapGrid()
    {
        var caps = new List<int>();
        foreach (DataGridViewRow row in DGV_LevelCaps.Rows)
        {
            if (row.IsNewRow) continue;
            if (int.TryParse(Convert.ToString(row.Cells[2].Value), out int v)) caps.Add(v);
        }
        return caps;
    }

    /// <summary>
    /// What the loaded ROM has room for, so the one-or-the-other limit is visible before it bites.
    /// </summary>
    private static string DescribeLevelCapSpace()
    {
        try
        {
            string code = pk3DS.Core.CTR.ExeFS.ResolveCodeBin(Main.Config?.ExeFS);
            if (!File.Exists(code)) return "";

            var report = CodeSpaceBudget.Measure(File.ReadAllBytes(code));
            string basis = $"code.bin has {report.Free} byte(s) of executable space; level caps use {CodeSpaceBudget.LevelCapBytes}.";

            if (!report.Fits(CodeSpaceBudget.LevelCapBytes))
                return basis + " There is not enough room - something else has already claimed it.";

            if (report.Free < CodeSpaceBudget.LevelCapBytes + CodeSpaceBudget.TMExpansionBytes)
                return basis + $" Installing them leaves {report.Free - CodeSpaceBudget.LevelCapBytes} byte(s), " +
                       $"which is not enough for the TM expansion ({CodeSpaceBudget.TMExpansionBytes}). " +
                       "This ROM has room for one or the other.";

            return basis + " There is also room for the TM expansion.";
        }
        catch { return ""; }
    }

    private void BuildTabItems()
    {
        var tab = new TabPage("Items");

        // Field Items
        var GB_FieldItems = new GroupBox { Text = "Field Items", Location = new Point(10, 10), Size = new Size(815, 115) };
        RB_FieldItemsUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_FieldItemsShuffle = new RadioButton { Text = "Shuffle", Location = new Point(15, 42), AutoSize = true };
        RB_FieldItemsRandom = new RadioButton { Text = "Random", Location = new Point(15, 64), AutoSize = true };
        RB_FieldItemsRandomEven = new RadioButton { Text = "Random (even distribution)", Location = new Point(15, 86), AutoSize = true };
        CHK_FieldItemsBanBad = new CheckBox { Text = "Ban Bad Items", Location = new Point(260, 20), AutoSize = true, Checked = true };
        GB_FieldItems.Controls.AddRange(new Control[] { RB_FieldItemsUnchanged, RB_FieldItemsShuffle, RB_FieldItemsRandom, RB_FieldItemsRandomEven, CHK_FieldItemsBanBad });

        // Special Shops
        var GB_Shops = new GroupBox { Text = "Special Shops", Location = new Point(10, 130), Size = new Size(815, 190) };
        RB_ShopsUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_ShopsShuffle = new RadioButton { Text = "Shuffle", Location = new Point(15, 50), AutoSize = true };
        RB_ShopsRandom = new RadioButton { Text = "Random", Location = new Point(15, 80), AutoSize = true };

        CHK_ShopsBanBad = new CheckBox { Text = "Ban Bad Items", Location = new Point(260, 20), AutoSize = true, Checked = true };
        CHK_ShopsBanRegular = new CheckBox { Text = "Ban Regular Shop Items", Location = new Point(260, 42), AutoSize = true };
        CHK_ShopsBanOverpowered = new CheckBox { Text = "Ban Overpowered Shop Items", Location = new Point(260, 64), AutoSize = true };
        CHK_ShopsBalancePrices = new CheckBox { Text = "Balance Shop Item Prices", Location = new Point(260, 86), AutoSize = true };
        CHK_GuaranteeEvolutionItems = new CheckBox { Text = "Guarantee Evolution Items", Location = new Point(260, 108), AutoSize = true };
        CHK_GuaranteeXItems = new CheckBox { Text = "Guarantee X Items", Location = new Point(260, 130), AutoSize = true };

        // Own full-width row below both columns — its label is long enough to run into the
        // right-hand column ("Guarantee Evolution Items" etc.) if placed side-by-side with them.
        CHK_ShopsRandomizeAll = new CheckBox { Text = "Randomize All Shops (don't skip unmapped locations)", Location = new Point(15, 158), AutoSize = true };
        GB_Shops.Controls.AddRange(new Control[] { RB_ShopsUnchanged, RB_ShopsShuffle, RB_ShopsRandom, CHK_ShopsBanBad, CHK_ShopsBanRegular, CHK_ShopsBanOverpowered, CHK_ShopsBalancePrices, CHK_GuaranteeEvolutionItems, CHK_GuaranteeXItems, CHK_ShopsRandomizeAll });

        // Pickup Items
        var GB_Pickup = new GroupBox { Text = "Pickup Items", Location = new Point(10, 330), Size = new Size(815, 95) };
        RB_PickupUnchanged = new RadioButton { Text = "Unchanged", Location = new Point(15, 20), AutoSize = true, Checked = true };
        RB_PickupRandom = new RadioButton { Text = "Random", Location = new Point(15, 50), AutoSize = true };
        CHK_PickupBanBad = new CheckBox { Text = "Ban Bad Items", Location = new Point(260, 20), AutoSize = true, Checked = true };
        GB_Pickup.Controls.AddRange(new Control[] { RB_PickupUnchanged, RB_PickupRandom, CHK_PickupBanBad });

        tab.Controls.AddRange(new Control[] { GB_FieldItems, GB_Shops, GB_Pickup });
        TC_Main.TabPages.Add(tab);
    }

    private void BuildTabMiscTweaks()
    {
        var tab = new TabPage("Misc. Tweaks");

        // 118 tall, not 380: its lowest control ends at 107, and the reclaimed space is what lets the
        // patch list sit below it rather than on top of it.
        var GB_Tweaks = new GroupBox { Text = "Misc. Tweaks", Location = new Point(10, 10), Size = new Size(815, 118) };

        CHK_BanLuckyEgg = new CheckBox { Text = "Ban Lucky Egg", Location = new Point(15, 25), AutoSize = true };
        CHK_BalanceStaticPokemonLevels = new CheckBox { Text = "Balance Static Pokemon Levels", Location = new Point(15, 55), AutoSize = true };

        CHK_NoFreeLuckyEgg = new CheckBox { Text = "No Free Lucky Egg", Location = new Point(260, 25), AutoSize = true };
        CHK_DontRevertTempAltFormes = new CheckBox { Text = "Don't Revert Temporary Alt Formes", Location = new Point(260, 55), AutoSize = true };

        CHK_FastestText = new CheckBox { Text = "Fastest Text", Location = new Point(530, 25), AutoSize = true };
        CHK_AllWildPokemonCanCallAllies = new CheckBox { Text = "All Wild Pokemon Can Call Allies", Location = new Point(530, 55), AutoSize = true };

        CHK_FastEggHatching = new CheckBox { Text = "Fast Egg Hatching", Location = new Point(15, 85), AutoSize = true };
        CHK_NoEVsFromPokemon = new CheckBox { Text = "No EVs From Pokemon", Location = new Point(260, 85), AutoSize = true };

        CHK_ExpMultiplier = new CheckBox { Text = "EXP Multiplier:", Location = new Point(530, 85), AutoSize = true };
        NUD_ExpMultiplier = new NumericUpDown { Location = new Point(642, 83), Size = new Size(50, 22), Minimum = 1, Maximum = 255, Value = 2 };
        L_ExpMultiplier = new Label { Text = "x", Location = new Point(695, 85), AutoSize = true };

        GB_Tweaks.Controls.AddRange(new Control[] { CHK_BanLuckyEgg, CHK_BalanceStaticPokemonLevels, CHK_NoFreeLuckyEgg, CHK_DontRevertTempAltFormes, CHK_FastestText, CHK_AllWildPokemonCanCallAllies, CHK_FastEggHatching, CHK_NoEVsFromPokemon, CHK_ExpMultiplier, NUD_ExpMultiplier, L_ExpMultiplier });

        var GB_Patches = new GroupBox { Text = "Research Center patches", Location = new Point(10, 136), Size = new Size(815, 282) };
        CLB_Patches = new CheckedListBox
        {
            Location = new Point(15, 22),
            Size = new Size(785, 175),
            CheckOnClick = true,
            IntegralHeight = false,
            HorizontalScrollbar = true,
        };

        // Fixed box rather than AutoSize. Provides enough space (665x72) for 3-4 line descriptions
        // while terminating comfortably before the buttons on the right side.
        L_PatchHint = new Label
        {
            Location = new Point(15, 203),
            Size = new Size(665, 72),
            ForeColor = Color.Gainsboro,
            AutoSize = false,
        };

        var B_PatchNone = new Button { Text = "Clear All", Location = new Point(690, 204), Size = new Size(110, 28) };
        B_PatchNone.Click += (s, e) =>
        {
            for (int i = 0; i < CLB_Patches.Items.Count; i++) CLB_Patches.SetItemChecked(i, false);
        };

        var B_PatchIds = new Button { Text = "Preview IDs", Location = new Point(690, 238), Size = new Size(110, 28) };
        B_PatchIds.Click += (s, e) => PreviewPatchIds();

        CLB_Patches.SelectedIndexChanged += (s, e) => ShowPatchHint();
        GB_Patches.Controls.AddRange(new Control[] { CLB_Patches, L_PatchHint, B_PatchNone, B_PatchIds });

        LoadPatchList();

        tab.Controls.Add(GB_Patches);
        tab.Controls.Add(GB_Tweaks);
        TC_Main.TabPages.Add(tab);
    }

    /// <summary>
    /// Fills the patch list from whatever the Research Center can see on this build.
    /// </summary>
    private void LoadPatchList()
    {
        CLB_Patches.Items.Clear();
        _patchRows.Clear();

        List<Recipe> book;
        try { book = Recipes.Discover(null, ResearchVersion.Resolve(Main.Config)); }
        catch { book = []; }

        if (book.Count == 0)
        {
            CLB_Patches.Items.Add("(no patches found - open a ROM, or check the patch-packages and other-ips folders)");
            _patchRows.Add(null);
            CLB_Patches.Enabled = false;
            return;
        }

        foreach (var group in book.GroupBy(r => r.Category).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            CLB_Patches.Items.Add($"—— {group.Key} ——");
            _patchRows.Add(null);

            foreach (var r in group.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                string idSummary = GetShortIdSummary(r);
                string slots = !string.IsNullOrEmpty(idSummary) ? $"  [{idSummary}]" : r.SlotCount > 0 ? $"  [{r.SlotCount} id{(r.SlotCount == 1 ? "" : "s")}]" : "";
                CLB_Patches.Items.Add($"    {r.Name}{slots}");
                _patchRows.Add(r);
            }
        }

        // A heading is not a patch, so ticking one would enable nothing; bounce the tick back.
        CLB_Patches.ItemCheck += (s, e) =>
        {
            if (e.Index >= 0 && e.Index < _patchRows.Count && _patchRows[e.Index] == null)
                e.NewValue = CheckState.Unchecked;
        };

        ShowPatchHint();
    }

    private static string GetShortIdSummary(Recipe r)
    {
        if (r.SlotCount == 0 && r.Package?.Parameters is not { Count: > 0 }) return "";
        if (Main.Config == null)
            return r.SlotCount > 0 ? $"{r.SlotCount} id{(r.SlotCount == 1 ? "" : "s")}" : "";

        try
        {
            var problems = new List<string>();
            var values = RecipeIdAllocator.AssignIds(r, Main.Config, problems);
            if (values is { Count: > 0 })
            {
                var parts = values.Values.Select(v =>
                {
                    var ids = v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return ids.Length > 1 ? $"{ids[0]}-{ids[^1]}" : v;
                });
                return "IDs: " + string.Join(", ", parts);
            }

            if (r.SlotCount > 0 && r.Entries.Count > 0)
            {
                int first = r.Entries[0].Id, last = r.Entries[^1].Id;
                return r.SlotCount == 1 ? $"ID: {first}" : $"IDs: {first}-{last}";
            }
        }
        catch { }

        return r.SlotCount > 0 ? $"{r.SlotCount} id{(r.SlotCount == 1 ? "" : "s")}" : "";
    }

    private void ShowPatchHint()
    {
        int i = CLB_Patches.SelectedIndex;
        if (i < 0 || i >= _patchRows.Count || _patchRows[i] == null)
        {
            L_PatchHint.Text = $"{_patchRows.Count(r => r != null)} patches available; ticked ones install at the end of the randomize. " +
                               "Items & abilities packages each need ~1.6KB of free space in Battle.cro and a stock ROM holds " +
                               "about four of them - the rest are reported and skipped, never half-written. " +
                               "Expanding Battle.cro's code segment is what raises that limit.";
            return;
        }

        var r = _patchRows[i];
        string files = string.Join(", ", r.ResolvedTargets);
        L_PatchHint.Text = $"{r.Summary}\r\nWrites {files}.{DescribeIds(r)}";
    }

    /// <summary>
    /// Which ids this recipe would claim on the loaded ROM, or why it cannot claim any.
    /// </summary>
    private static string DescribeIds(Recipe r)
    {
        if (r.SlotCount == 0 && r.Package?.Parameters is not { Count: > 0 }) return "";
        if (Main.Config == null) return "  Load a ROM to see which item IDs it would take.";

        try
        {
            // Preview on a copy: AssignIds writes into Entries, and the list must not be mutated by
            // merely looking at a row.
            var problems = new List<string>();
            var values = RecipeIdAllocator.AssignIds(r, Main.Config, problems);
            if (problems.Count > 0) return $"  No free IDs: {problems[0]}.";

            if (values is { Count: > 0 })
            {
                var parts = values.Select(kv =>
                {
                    var ids = kv.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return ids.Length > 3
                        ? $"{kv.Key}={ids[0]}-{ids[^1]} ({ids.Length})"
                        : $"{kv.Key}={kv.Value}";
                });
                return "  IDs assigned automatically: " + string.Join(", ", parts) + ".";
            }

            if (r.SlotCount > 0 && r.Entries.Count > 0)
            {
                int first = r.Entries[0].Id, last = r.Entries[^1].Id;
                return r.SlotCount == 1
                    ? $"  ID assigned automatically: {first}."
                    : $"  IDs assigned automatically: {first}-{last}.";
            }
        }
        catch { }
        return "";
    }

    /// <summary>Reports the ids every ticked patch would take, in one place.</summary>
    private void PreviewPatchIds()
    {
        var picked = ReadPatchList();
        if (picked.Count == 0) { WinFormsUtil.Alert("Tick some patches first."); return; }
        if (Main.Config == null) { WinFormsUtil.Alert("Load a ROM first."); return; }

        var lines = new List<string>();
        foreach (var r in _patchRows.Where(x => x != null && picked.Contains(x.Name)))
        {
            string ids = DescribeIds(r).Trim();
            lines.Add(ids.Length == 0 ? $"{r.Name}: no IDs needed" : $"{r.Name}: {ids}");
        }
        WinFormsUtil.Alert("These IDs are assigned automatically when you randomize:\n\n" +
                           string.Join("\n", lines));
    }

    /// <summary>Names of the ticked patches, in list order.</summary>
    private List<string> ReadPatchList()
    {
        var picked = new List<string>();
        foreach (int i in CLB_Patches.CheckedIndices)
            if (i >= 0 && i < _patchRows.Count && _patchRows[i] != null)
                picked.Add(_patchRows[i].Name);
        return picked;
    }

    private void ApplyPatchList(List<string> names)
    {
        var want = new HashSet<string>(names ?? [], StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _patchRows.Count && i < CLB_Patches.Items.Count; i++)
            CLB_Patches.SetItemChecked(i, _patchRows[i] != null && want.Contains(_patchRows[i].Name));
    }

    private UniversalSettings GetSettingsFromUI()
    {
        return new UniversalSettings
        {
            Seed = TB_Seed.Text,
            LimitPokemon = CHK_LimitPokemon.Checked,
            CompetitiveRandomizer = CHK_CompetitiveRandomizer.Checked,
            NoIrregularAltFormes = CHK_NoIrregularAltFormes.Checked,
            RaceMode = CHK_RaceMode.Checked,

            Gen1 = CHK_LimitPokemon.Checked ? _limitGen1 : true,
            Gen2 = CHK_LimitPokemon.Checked ? _limitGen2 : true,
            Gen3 = CHK_LimitPokemon.Checked ? _limitGen3 : true,
            Gen4 = CHK_LimitPokemon.Checked ? _limitGen4 : true,
            Gen5 = CHK_LimitPokemon.Checked ? _limitGen5 : true,
            Gen6 = CHK_LimitPokemon.Checked ? _limitGen6 : true,
            Gen7 = CHK_LimitPokemon.Checked ? _limitGen7 : true,
            Gen8 = CHK_LimitPokemon.Checked ? _limitGen8 : true,
            Gen9 = CHK_LimitPokemon.Checked ? _limitGen9 : true,
            AllowLegendaries = CHK_LimitPokemon.Checked ? _limitAllowLegendaries : true,
            AllowMythicals = CHK_LimitPokemon.Checked ? _limitAllowMythicals : true,

            // Tab 1
            BaseStatsMode = RB_StatsUnchanged.Checked ? 0 : (RB_StatsShuffle.Checked ? 1 : 2),
            StatVariancePercent = TB_StatVariance.Value,
            BaseStatsFollowEvolutions = CHK_StatsFollowEvolutions.Checked,
            BaseStatsFollowMegas = CHK_StatsFollowMegas.Checked,
            RandomizeAddedStatsOnEvo = CHK_StatsRandomizeAddedStats.Checked,
            UpdateBaseStatsToGen = CHK_StatsUpdateToGen.Checked,
            UpdateBaseStatsGenIndex = CB_StatsUpdateGen.SelectedIndex + 1,
            StandardizeExpCurves = CHK_StatsStandardizeExp.Checked,
            StandardizeExpCurveTarget = CB_StatsStandardizeExpTarget.SelectedIndex,
            ExpCurvePokemonScope = RB_ExpLegendariesSlow.Checked ? 0 : (RB_ExpStrongLegendariesSlow.Checked ? 1 : 2),

            EnforceMinimumBST = _enforceMinBST,
            MinBST3Stage1 = _minBST3Stage1,
            MinBST3Stage2 = _minBST3Stage2,
            MinBST3Stage3 = _minBST3Stage3,
            MinBST2Stage1 = _minBST2Stage1,
            MinBST2Stage2 = _minBST2Stage2,
            MinBST1Stage = _minBST1Stage,
            MinBSTLegendary = _minBSTLegendary,

            EnforceMaximumBST = _enforceMaxBST,
            MaxBST3Stage1 = _maxBST3Stage1,
            MaxBST3Stage2 = _maxBST3Stage2,
            MaxBST3Stage3 = _maxBST3Stage3,
            MaxBST2Stage1 = _maxBST2Stage1,
            MaxBST2Stage2 = _maxBST2Stage2,
            MaxBST1Stage = _maxBST1Stage,
            MaxBSTLegendary = _maxBSTLegendary,

            NoEgregiousStats = _noEgregiousStats,
            NoEgregiousStatsSingleCap = _noEgregiousStatsSingleCap,
            NoEgregiousStatsBSTCapRegular = _noEgregiousStatsBSTCapRegular,
            NoEgregiousStatsBSTCapLegendary = _noEgregiousStatsBSTCapLegendary,

            AvoidMinmaxing = _avoidMinmaxing,

            TypesMode = RB_TypesUnchanged.Checked ? 0 : (RB_TypesRandomEvos.Checked ? 1 : 2),
            TypesFollowMegas = CHK_TypesFollowMegas.Checked,
            ForceDualTypes = CHK_TypesForceDual.Checked,

            TypeEffectivenessMode =
                RB_TypeEffRandom.Checked ? TypeEffectivenessRandomizer.Random :
                RB_TypeEffBalanced.Checked ? TypeEffectivenessRandomizer.RandomBalanced :
                RB_TypeEffKeepIdentities.Checked ? TypeEffectivenessRandomizer.KeepTypeIdentities :
                RB_TypeEffInverse.Checked ? TypeEffectivenessRandomizer.Inverse :
                TypeEffectivenessRandomizer.Unchanged,
            TypeEffectivenessAddRandomImmunities = CHK_TypeEffAddImmunities.Checked,

            AbilitiesMode = RB_AbilUnchanged.Checked ? 0 : 1,
            AllowWonderGuard = CHK_AbilAllowWonderGuard.Checked,
            AbilitiesFollowEvolutions = CHK_AbilFollowEvolutions.Checked,
            AbilitiesFollowMegas = CHK_AbilFollowMegas.Checked,
            BanTrappingAbilities = CHK_AbilTrapping.Checked,
            CombineDuplicateAbilities = CHK_AbilCombineDuplicate.Checked,
            BanNegativeAbilities = CHK_AbilNegative.Checked,
            BanBadAbilities = CHK_AbilBad.Checked,
            BannedAbilityNames = [.. _bannedAbilityNames],
            EnsureTwoAbilities = CHK_AbilEnsureTwo.Checked,

            EvolutionsMode = RB_EvosUnchanged.Checked ? 0 : (RB_EvosRandom.Checked ? 1 : 2),
            EvosSimilarStrength = CHK_EvosSimilarStrength.Checked,
            EvosSameTyping = CHK_EvosSameTyping.Checked,
            LimitEvosTo3Stages = CHK_EvosLimitThreeStages.Checked,
            EvosForceChange = CHK_EvosForceChange.Checked,
            EvosAllowAltFormes = CHK_EvosAllowAltFormes.Checked,
            ChangeImpossibleEvos = CHK_EvosChangeImpossible.Checked,
            MakeEvosEasier = CHK_EvosMakeEasier.Checked,
            RemoveTimeBasedEvos = CHK_EvosRemoveTimeBased.Checked,

            // Tab 2
            StartersMode = RB_StartersUnchanged.Checked ? 0 : (RB_StartersCustom.Checked ? 1 : (RB_StartersRandomCompletely.Checked ? 2 : 3)),
            StarterTypeRestriction = RB_StarterTypeFWG.Checked ? 1 : (RB_StarterTypeTriangle.Checked ? 2 : (RB_StarterTypeUnique.Checked ? 3 : (RB_StarterTypeSingle.Checked ? 4 : 0))),
            StarterSingleType = Math.Max(0, CB_StarterSingleType.SelectedIndex),
            StarterNoDualTypes = CHK_StarterNoDualTypes.Checked,
            CustomStarter1 = CB_Starter1.SelectedIndex + 1,
            CustomStarter2 = CB_Starter2.SelectedIndex + 1,
            CustomStarter3 = CB_Starter3.SelectedIndex + 1,
            RandomizeStarterHeldItems = CHK_StarterHeldItems.Checked,
            StartersBanBadItems = CHK_StarterBanBadItems.Checked,
            StartersAllowAltFormes = CHK_StarterAllowAltFormes.Checked,

            StaticsMode = RB_StaticsUnchanged.Checked ? 0 : (RB_StaticsSwap.Checked ? 1 : (RB_StaticsRandomCompletely.Checked ? 2 : 3)),
            StaticsRandomize600BST = CHK_Statics600BST.Checked,
            LimitMainGameLegendaries = CHK_StaticsLimitMainGame.Checked,
            StaticsAllowAltFormes = CHK_StaticsAllowAltFormes.Checked,
            StaticsSwapMegaEvolvables = CHK_StaticsSwapMega.Checked,
            StaticsFixMusic = CHK_StaticsFixMusic.Checked,
            StaticsLevelModifierPercent = TB_StaticsLevelMod.Value,

            TradesMode = RB_TradesUnchanged.Checked ? 0 : (RB_TradesGivenOnly.Checked ? 1 : 2),
            TradesRandomizeNicknames = CHK_TradesNicknames.Checked,
            TradesRandomizeOTs = CHK_TradesOTs.Checked,
            TradesRandomizeIVs = CHK_TradesIVs.Checked,
            TradesRandomizeItems = CHK_TradesItems.Checked,

            // Tab 3
            RandomizeMovePower = CHK_MovePower.Checked,
            RandomizeMoveAccuracy = CHK_MoveAccuracy.Checked,
            RandomizeMovePP = CHK_MovePP.Checked,
            RandomizeMoveTypes = CHK_MoveTypes.Checked,
            RandomizeMoveCategory = CHK_MoveCategory.Checked,
            UpdateMovesToGen = CHK_MoveUpdateToGen.Checked,
            UpdateMovesGenIndex = CB_MoveUpdateGen.SelectedIndex + 1,

            MovesetsMode = RB_MovesetsUnchanged.Checked ? 0 : (RB_MovesetsSameType.Checked ? 1 : (RB_MovesetsCompletely.Checked ? 2 : 3)),
            GuaranteedLevel1Moves = CHK_GuaranteedLv1Moves.Checked,
            GuaranteedLevel1MovesCount = TB_GuaranteedLv1Count.Value,
            ReorderDamagingMoves = CHK_ReorderDamagingMoves.Checked,
            NoGameBreakingMoves = CHK_NoGameBreakingMoves.Checked,
            ForceGoodDamagingMoves = CHK_ForcePercentGoodMoves.Checked,
            ForceGoodDamagingMovesPercent = TB_ForcePercentGoodMovesVal.Value,
            EvolutionMovesForAll = CHK_EvoMovesForAll.Checked,

            // Tab 4
            TrainerPokemonMode = Math.Max(0, CB_TrainerMode.SelectedIndex),
            TrainerAvoidDuplicates = CHK_TrainerAvoidDuplicates.Checked,
            TrainerDiverseTypesBoss = CHK_DiverseBoss.Checked,
            TrainerDiverseTypesImportant = CHK_DiverseImportant.Checked,
            TrainerDiverseTypesRegular = CHK_DiverseRegular.Checked,

            DoubleBattleMode = CHK_TrainerDoubleBattle.Checked,
            BattleStyleMode = RB_StyleRandom.Checked ? 1 : (RB_StyleSingle.Checked ? 2 : 0),
            BattleStyleChoice = Math.Max(0, CB_BattleStyle.SelectedIndex),
            BetterTrainerMovesets = CHK_TrainerBetterMovesets.Checked,
            RivalCarriesStarter = CHK_RivalCarriesStarter.Checked,
            TrainerTrySimilarStrength = CHK_TrainerSimilarStrength.Checked,
            WeightTypesByNumPokemon = CHK_TrainerWeightTypes.Checked,
            TrainerDontUseLegendaries = CHK_TrainerNoLegendaries.Checked,
            NoEarlyWonderGuard = CHK_TrainerNoEarlyWonderGuard.Checked,
            TrainerAllowAltFormes = CHK_TrainerAllowAltFormes.Checked,
            TrainerSwapMegaEvolvables = CHK_TrainerSwapMega.Checked,
            TrainerRandomShiny = CHK_TrainerRandomShiny.Checked,
            RandomizeTrainerNames = CHK_RandomizeTrainerNames.Checked,
            RandomizeTrainerClassNames = CHK_RandomizeTrainerClassNames.Checked,
            NoMegaEvolution = CHK_TrainerNoMegaEvolution.Checked,
            NoZMoves = CHK_TrainerNoZMoves.Checked,
            TrainerFullIVs = CHK_TrainerFullIVs.Checked,

            AddPokemonBossTrainers = CHK_AddBossPk.Checked,
            AddPokemonBossCount = (int)NUD_AddBossCount.Value,
            AddPokemonImportantTrainers = CHK_AddImportantPk.Checked,
            AddPokemonImportantCount = (int)NUD_AddImportantCount.Value,
            AddPokemonRegularTrainers = CHK_AddRegularPk.Checked,
            AddPokemonRegularCount = (int)NUD_AddRegularCount.Value,

            AddHeldItemsBossTrainers = CHK_ItemBossPk.Checked,
            AddHeldItemsImportantTrainers = CHK_ItemImportantPk.Checked,
            AddHeldItemsRegularTrainers = CHK_ItemRegularPk.Checked,

            ForceFullyEvolvedAtLevel = CHK_ForceFullyEvolved.Checked,
            ForceFullyEvolvedLevel = TB_ForceFullyEvolvedLevel.Value,
            TrainerLevelModifierPercent = TB_TrainerLevelMod.Value,

            TotemMode = RB_TotemUnchanged.Checked ? 0 : (RB_TotemRandom.Checked ? 1 : 2),
            TotemAllyMode = RB_AllyUnchanged.Checked ? 0 : (RB_AllyRandom.Checked ? 1 : 2),
            TotemAuraMode = RB_AuraUnchanged.Checked ? 0 : (RB_AuraRandom.Checked ? 1 : 2),
            TotemRandomizeHeldItems = CHK_TotemHeldItems.Checked,
            TotemAllowAltFormes = CHK_TotemAllowAltFormes.Checked,
            TotemLevelModifierPercent = TB_TotemLevelMod.Value,

            // Tab 5
            WildPokemonMode = RB_WildUnchanged.Checked ? 0 : (RB_WildRandom.Checked ? 1 : (RB_WildArea1To1.Checked ? 2 : 3)),
            WildAdditionalRule = RB_WildRuleNone.Checked ? 0 : (RB_WildRuleSimilar.Checked ? 1 : (RB_WildRuleCatchEmAll.Checked ? 2 : 3)),
            WildUseTimeBased = CHK_WildTimeBased.Checked,
            WildDontUseLegendaries = CHK_WildNoLegendaries.Checked,
            WildSetMinCatchRate = CHK_WildMinCatchRate.Checked,
            WildMinCatchRate = TB_WildMinCatchRateVal.Value,
            WildRandomizeHeldItems = CHK_WildHeldItems.Checked,
            WildBanBadItems = CHK_WildBanBadItems.Checked,
            WildBalanceShakingGrass = CHK_WildShakingGrass.Checked,
            WildLevelModifierPercent = TB_WildLevelMod.Value,
            WildAllowAltFormes = CHK_WildAllowAltFormes.Checked,

            WildReplacementsPerSpecies = Math.Max(0, CB_WildReplacements.SelectedIndex),
            WildSplitByEncounterType = CHK_WildSplitByEncounterType.Checked,
            WildAvoidRepeats = CHK_WildAvoidRepeats.Checked,
            WildTypeRestriction =
                RB_WildTypeZoneThemes.Checked ? WildRestrictions.TypeRandomZoneThemes :
                RB_WildTypeKeepPrimary.Checked ? WildRestrictions.TypeKeepPrimary :
                WildRestrictions.TypeNone,
            WildKeepZoneThemes = CHK_WildKeepZoneThemes.Checked,
            WildEvolutionRestriction =
                RB_WildEvoOnlyBasic.Checked ? WildRestrictions.EvoOnlyBasic :
                RB_WildEvoSameStage.Checked ? WildRestrictions.EvoSameStage :
                WildRestrictions.EvoNone,
            WildKeepEvolutionRelations = CHK_WildKeepEvoRelations.Checked,

            // Tab 6
            EnableLevelCaps = CHK_LevelCaps.Checked,
            CheapRareCandies = CHK_CheapCandies.Checked,
            LevelCapShift = LevelCapShifts.All[Math.Clamp(CB_LevelCapStyle.SelectedIndex, 0, LevelCapShifts.All.Length - 1)],
            LevelCapMatchTrainers = CB_LevelCapStyle.SelectedIndex >= LevelCapShifts.All.Length,
            LevelCapFinal = (int)NUD_LevelCapFinal.Value,
            LevelCapCaps = ReadCapGrid(),

            TMHMMovesMode = RB_TMMovesUnchanged.Checked ? 0 : (RB_TMMovesRandom.Checked ? 1 : 2),
            KeepFieldMoveTMs = CHK_KeepFieldTMs.Checked,
            TMForcePercentGoodMoves = CHK_TMForceGoodMoves.Checked,
            TMForcePercentGoodMovesValue = TB_TMForceGoodMovesVal.Value,

            TMHMCompatibilityMode = RB_TMCompUnchanged.Checked ? 0 : (RB_TMCompSameType.Checked ? 1 : (RB_TMCompCompletely.Checked ? 2 : 3)),
            TMLevelupMoveSanity = CHK_TMSanity.Checked,
            TMFollowEvolutions = CHK_TMFollowEvos.Checked,
            FullHMCompatibility = CHK_FullHMComp.Checked,

            MoveTutorMovesMode = RB_TutorMovesUnchanged.Checked ? 0 : (RB_TutorMovesRandom.Checked ? 1 : 2),
            KeepFieldMoveTutors = CHK_KeepFieldTutors.Checked,
            TutorForcePercentGoodMoves = CHK_TutorForceGoodMoves.Checked,
            TutorForcePercentGoodMovesValue = TB_TutorForceGoodMovesVal.Value,

            MoveTutorCompatibilityMode = RB_TutorCompUnchanged.Checked ? 0 : (RB_TutorCompSameType.Checked ? 1 : (RB_TutorCompCompletely.Checked ? 2 : 3)),
            TutorLevelupMoveSanity = CHK_TutorSanity.Checked,
            TutorFollowEvolutions = CHK_TutorFollowEvos.Checked,

            // Tab 7
            FieldItemsMode = RB_FieldItemsUnchanged.Checked ? 0 : (RB_FieldItemsShuffle.Checked ? 1 : (RB_FieldItemsRandom.Checked ? 2 : 3)),
            FieldItemsBanBadItems = CHK_FieldItemsBanBad.Checked,

            SpecialShopsMode = RB_ShopsUnchanged.Checked ? 0 : (RB_ShopsShuffle.Checked ? 1 : 2),
            RandomizeAllShops = CHK_ShopsRandomizeAll.Checked,
            ShopBanBadItems = CHK_ShopsBanBad.Checked,
            ShopBanRegularItems = CHK_ShopsBanRegular.Checked,
            ShopBanOverpoweredItems = CHK_ShopsBanOverpowered.Checked,
            BalanceShopItemPrices = CHK_ShopsBalancePrices.Checked,
            GuaranteeEvolutionItems = CHK_GuaranteeEvolutionItems.Checked,
            GuaranteeXItems = CHK_GuaranteeXItems.Checked,

            PickupItemsMode = RB_PickupUnchanged.Checked ? 0 : 1,
            PickupBanBadItems = CHK_PickupBanBad.Checked,

            // Tab 8
            BanLuckyEgg = CHK_BanLuckyEgg.Checked,
            BalanceStaticPokemonLevels = CHK_BalanceStaticPokemonLevels.Checked,
            NoFreeLuckyEgg = CHK_NoFreeLuckyEgg.Checked,
            DontRevertTempAltFormes = CHK_DontRevertTempAltFormes.Checked,
            FastestText = CHK_FastestText.Checked,
            AllWildPokemonCanCallAllies = CHK_AllWildPokemonCanCallAllies.Checked,
            FastEggHatching = CHK_FastEggHatching.Checked,
            NoEVsFromPokemon = CHK_NoEVsFromPokemon.Checked,
            CustomExpMultiplier = CHK_ExpMultiplier.Checked,
            ExpMultiplier = (int)NUD_ExpMultiplier.Value,
            InstallPatches = ReadPatchList(),
        };
    }

    private void LoadSettingsToUI(UniversalSettings s)
    {
        TB_Seed.Text = s.Seed;
        CHK_LimitPokemon.Checked = s.LimitPokemon;
        CHK_CompetitiveRandomizer.Checked = s.CompetitiveRandomizer;
        L_ModeIndicator.Text = s.CompetitiveRandomizer ? "Mode: Competitive" : "Mode: Regular";
        CHK_NoIrregularAltFormes.Checked = s.NoIrregularAltFormes;
        CHK_RaceMode.Checked = s.RaceMode;

        _limitGen1 = s.Gen1;
        _limitGen2 = s.Gen2;
        _limitGen3 = s.Gen3;
        _limitGen4 = s.Gen4;
        _limitGen5 = s.Gen5;
        _limitGen6 = s.Gen6;
        _limitGen7 = s.Gen7;
        _limitGen8 = s.Gen8;
        _limitGen9 = s.Gen9;
        _limitAllowLegendaries = s.AllowLegendaries;
        _limitAllowMythicals = s.AllowMythicals;

        // Tab 1
        RB_StatsUnchanged.Checked = s.BaseStatsMode == 0;
        RB_StatsShuffle.Checked = s.BaseStatsMode == 1;
        RB_StatsRandom.Checked = s.BaseStatsMode == 2;
        TB_StatVariance.Value = Math.Max(5, Math.Min(75, s.StatVariancePercent == 0 ? 25 : s.StatVariancePercent));
        L_StatVarianceVal.Text = $"{TB_StatVariance.Value}%";
        CHK_StatsFollowEvolutions.Checked = s.BaseStatsFollowEvolutions;
        CHK_StatsFollowMegas.Checked = s.BaseStatsFollowMegas;
        CHK_StatsRandomizeAddedStats.Checked = s.RandomizeAddedStatsOnEvo;
        CHK_StatsUpdateToGen.Checked = s.UpdateBaseStatsToGen;
        CB_StatsUpdateGen.SelectedIndex = Math.Max(0, Math.Min(8, s.UpdateBaseStatsGenIndex - 1));
        CHK_StatsStandardizeExp.Checked = s.StandardizeExpCurves;
        CB_StatsStandardizeExpTarget.SelectedIndex = Math.Max(0, Math.Min(5, s.StandardizeExpCurveTarget));
        RB_ExpLegendariesSlow.Checked = s.ExpCurvePokemonScope == 0;
        RB_ExpStrongLegendariesSlow.Checked = s.ExpCurvePokemonScope == 1;
        RB_ExpAllPokemon.Checked = s.ExpCurvePokemonScope == 2;

        _enforceMinBST = s.EnforceMinimumBST;
        _minBST3Stage1 = s.MinBST3Stage1;
        _minBST3Stage2 = s.MinBST3Stage2;
        _minBST3Stage3 = s.MinBST3Stage3;
        _minBST2Stage1 = s.MinBST2Stage1;
        _minBST2Stage2 = s.MinBST2Stage2;
        _minBST1Stage = s.MinBST1Stage;
        _minBSTLegendary = s.MinBSTLegendary;

        _enforceMaxBST = s.EnforceMaximumBST;
        _maxBST3Stage1 = s.MaxBST3Stage1;
        _maxBST3Stage2 = s.MaxBST3Stage2;
        _maxBST3Stage3 = s.MaxBST3Stage3;
        _maxBST2Stage1 = s.MaxBST2Stage1;
        _maxBST2Stage2 = s.MaxBST2Stage2;
        _maxBST1Stage = s.MaxBST1Stage;
        _maxBSTLegendary = s.MaxBSTLegendary;

        _noEgregiousStats = s.NoEgregiousStats;
        _noEgregiousStatsSingleCap = s.NoEgregiousStatsSingleCap;
        _noEgregiousStatsBSTCapRegular = s.NoEgregiousStatsBSTCapRegular;
        _noEgregiousStatsBSTCapLegendary = s.NoEgregiousStatsBSTCapLegendary;

        _avoidMinmaxing = s.AvoidMinmaxing;

        RB_TypesUnchanged.Checked = s.TypesMode == 0;
        RB_TypesRandomEvos.Checked = s.TypesMode == 1;
        RB_TypesRandomCompletely.Checked = s.TypesMode == 2;
        CHK_TypesFollowMegas.Checked = s.TypesFollowMegas;
        CHK_TypesForceDual.Checked = s.ForceDualTypes;

        RB_TypeEffUnchanged.Checked = s.TypeEffectivenessMode == TypeEffectivenessRandomizer.Unchanged;
        RB_TypeEffRandom.Checked = s.TypeEffectivenessMode == TypeEffectivenessRandomizer.Random;
        RB_TypeEffBalanced.Checked = s.TypeEffectivenessMode == TypeEffectivenessRandomizer.RandomBalanced;
        RB_TypeEffKeepIdentities.Checked = s.TypeEffectivenessMode == TypeEffectivenessRandomizer.KeepTypeIdentities;
        RB_TypeEffInverse.Checked = s.TypeEffectivenessMode == TypeEffectivenessRandomizer.Inverse;
        CHK_TypeEffAddImmunities.Checked = s.TypeEffectivenessAddRandomImmunities;
        CHK_TypeEffAddImmunities.Enabled = RB_TypeEffInverse.Checked;

        RB_AbilUnchanged.Checked = s.AbilitiesMode == 0;
        RB_AbilRandom.Checked = s.AbilitiesMode == 1;
        CHK_AbilAllowWonderGuard.Checked = s.AllowWonderGuard;
        CHK_AbilFollowEvolutions.Checked = s.AbilitiesFollowEvolutions;
        CHK_AbilFollowMegas.Checked = s.AbilitiesFollowMegas;
        CHK_AbilTrapping.Checked = s.BanTrappingAbilities;
        CHK_AbilCombineDuplicate.Checked = s.CombineDuplicateAbilities;
        CHK_AbilNegative.Checked = s.BanNegativeAbilities;
        CHK_AbilBad.Checked = s.BanBadAbilities;
        _bannedAbilityNames = s.BannedAbilityNames is { Count: > 0 } ? [.. s.BannedAbilityNames] : [];
        CHK_AbilEnsureTwo.Checked = s.EnsureTwoAbilities;

        RB_EvosUnchanged.Checked = s.EvolutionsMode == 0;
        RB_EvosRandom.Checked = s.EvolutionsMode == 1;
        RB_EvosRandomEveryLevel.Checked = s.EvolutionsMode == 2;
        CHK_EvosSimilarStrength.Checked = s.EvosSimilarStrength;
        CHK_EvosSameTyping.Checked = s.EvosSameTyping;
        CHK_EvosLimitThreeStages.Checked = s.LimitEvosTo3Stages;
        CHK_EvosForceChange.Checked = s.EvosForceChange;
        CHK_EvosAllowAltFormes.Checked = s.EvosAllowAltFormes;
        CHK_EvosChangeImpossible.Checked = s.ChangeImpossibleEvos;
        CHK_EvosMakeEasier.Checked = s.MakeEvosEasier;
        CHK_EvosRemoveTimeBased.Checked = s.RemoveTimeBasedEvos;

        // Tab 2
        RB_StartersUnchanged.Checked = s.StartersMode == 0;
        RB_StartersCustom.Checked = s.StartersMode == 1;
        RB_StartersRandomCompletely.Checked = s.StartersMode == 2;
        RB_StarterTypeNone.Checked = s.StarterTypeRestriction == 0;
        RB_StarterTypeFWG.Checked = s.StarterTypeRestriction == 1;
        RB_StarterTypeTriangle.Checked = s.StarterTypeRestriction == 2;
        RB_StarterTypeUnique.Checked = s.StarterTypeRestriction == 3;
        RB_StarterTypeSingle.Checked = s.StarterTypeRestriction == 4;
        CB_StarterSingleType.SelectedIndex = Math.Clamp(s.StarterSingleType, 0, TypeNames.Length - 1);
        CHK_StarterNoDualTypes.Checked = s.StarterNoDualTypes;
        RB_StartersRandomBasic.Checked = s.StartersMode == 3;
        CB_Starter1.SelectedIndex = Math.Max(0, Math.Min(1024, s.CustomStarter1 - 1));
        CB_Starter2.SelectedIndex = Math.Max(0, Math.Min(1024, s.CustomStarter2 - 1));
        CB_Starter3.SelectedIndex = Math.Max(0, Math.Min(1024, s.CustomStarter3 - 1));
        CHK_StarterHeldItems.Checked = s.RandomizeStarterHeldItems;
        CHK_StarterBanBadItems.Checked = s.StartersBanBadItems;
        CHK_StarterAllowAltFormes.Checked = s.StartersAllowAltFormes;

        RB_StaticsUnchanged.Checked = s.StaticsMode == 0;
        RB_StaticsSwap.Checked = s.StaticsMode == 1;
        RB_StaticsRandomCompletely.Checked = s.StaticsMode == 2;
        RB_StaticsRandomSimilar.Checked = s.StaticsMode == 3;
        CHK_Statics600BST.Checked = s.StaticsRandomize600BST;
        CHK_StaticsLimitMainGame.Checked = s.LimitMainGameLegendaries;
        CHK_StaticsAllowAltFormes.Checked = s.StaticsAllowAltFormes;
        CHK_StaticsSwapMega.Checked = s.StaticsSwapMegaEvolvables;
        CHK_StaticsFixMusic.Checked = s.StaticsFixMusic;
        TB_StaticsLevelMod.Value = Math.Clamp(s.StaticsLevelModifierPercent, -100, 100);
        L_StaticsLevelModVal.Text = $"{TB_StaticsLevelMod.Value}%";

        RB_TradesUnchanged.Checked = s.TradesMode == 0;
        RB_TradesGivenOnly.Checked = s.TradesMode == 1;
        RB_TradesBoth.Checked = s.TradesMode == 2;
        CHK_TradesNicknames.Checked = s.TradesRandomizeNicknames;
        CHK_TradesOTs.Checked = s.TradesRandomizeOTs;
        CHK_TradesIVs.Checked = s.TradesRandomizeIVs;
        CHK_TradesItems.Checked = s.TradesRandomizeItems;

        // Tab 3
        CHK_MovePower.Checked = s.RandomizeMovePower;
        CHK_MoveAccuracy.Checked = s.RandomizeMoveAccuracy;
        CHK_MovePP.Checked = s.RandomizeMovePP;
        CHK_MoveTypes.Checked = s.RandomizeMoveTypes;
        CHK_MoveCategory.Checked = s.RandomizeMoveCategory;
        CHK_MoveUpdateToGen.Checked = s.UpdateMovesToGen;
        CB_MoveUpdateGen.SelectedIndex = Math.Max(0, Math.Min(8, s.UpdateMovesGenIndex - 1));

        RB_MovesetsUnchanged.Checked = s.MovesetsMode == 0;
        RB_MovesetsSameType.Checked = s.MovesetsMode == 1;
        RB_MovesetsCompletely.Checked = s.MovesetsMode == 2;
        RB_MovesetsMetronome.Checked = s.MovesetsMode == 3;
        CHK_GuaranteedLv1Moves.Checked = s.GuaranteedLevel1Moves;
        TB_GuaranteedLv1Count.Value = Math.Max(2, Math.Min(4, s.GuaranteedLevel1MovesCount));
        L_GuaranteedLv1Val.Text = TB_GuaranteedLv1Count.Value.ToString();
        CHK_ReorderDamagingMoves.Checked = s.ReorderDamagingMoves;
        CHK_NoGameBreakingMoves.Checked = s.NoGameBreakingMoves;
        CHK_ForcePercentGoodMoves.Checked = s.ForceGoodDamagingMoves;
        TB_ForcePercentGoodMovesVal.Value = Math.Max(0, Math.Min(100, s.ForceGoodDamagingMovesPercent));
        L_ForcePercentGoodVal.Text = $"{TB_ForcePercentGoodMovesVal.Value}%";
        CHK_EvoMovesForAll.Checked = s.EvolutionMovesForAll;

        // Tab 4
        CB_TrainerMode.SelectedIndex = Math.Clamp(s.TrainerPokemonMode, 0, CB_TrainerMode.Items.Count - 1);
        CHK_TrainerAvoidDuplicates.Checked = s.TrainerAvoidDuplicates;
        CHK_DiverseBoss.Checked = s.TrainerDiverseTypesBoss;
        CHK_DiverseImportant.Checked = s.TrainerDiverseTypesImportant;
        CHK_DiverseRegular.Checked = s.TrainerDiverseTypesRegular;

        CHK_TrainerDoubleBattle.Checked = s.DoubleBattleMode;
        RB_StyleUnchanged.Checked = s.BattleStyleMode == 0;
        RB_StyleRandom.Checked = s.BattleStyleMode == 1;
        RB_StyleSingle.Checked = s.BattleStyleMode == 2;
        CB_BattleStyle.SelectedIndex = Math.Clamp(s.BattleStyleChoice, 0, CB_BattleStyle.Items.Count - 1);
        CB_BattleStyle.Enabled = RB_StyleSingle.Checked;
        CHK_TrainerBetterMovesets.Checked = s.BetterTrainerMovesets;
        CHK_RivalCarriesStarter.Checked = s.RivalCarriesStarter;
        CHK_TrainerSimilarStrength.Checked = s.TrainerTrySimilarStrength;
        CHK_TrainerWeightTypes.Checked = s.WeightTypesByNumPokemon;
        CHK_TrainerNoLegendaries.Checked = s.TrainerDontUseLegendaries;
        CHK_TrainerNoEarlyWonderGuard.Checked = s.NoEarlyWonderGuard;
        CHK_TrainerAllowAltFormes.Checked = s.TrainerAllowAltFormes;
        CHK_TrainerSwapMega.Checked = s.TrainerSwapMegaEvolvables;
        CHK_TrainerRandomShiny.Checked = s.TrainerRandomShiny;
        CHK_RandomizeTrainerNames.Checked = s.RandomizeTrainerNames;
        CHK_RandomizeTrainerClassNames.Checked = s.RandomizeTrainerClassNames;
        CHK_TrainerNoMegaEvolution.Checked = s.NoMegaEvolution;
        CHK_TrainerNoZMoves.Checked = s.NoZMoves;
        CHK_TrainerFullIVs.Checked = s.TrainerFullIVs;

        CHK_AddBossPk.Checked = s.AddPokemonBossTrainers;
        NUD_AddBossCount.Value = Math.Max(1, Math.Min(6, s.AddPokemonBossCount));
        CHK_AddImportantPk.Checked = s.AddPokemonImportantTrainers;
        NUD_AddImportantCount.Value = Math.Max(1, Math.Min(6, s.AddPokemonImportantCount));
        CHK_AddRegularPk.Checked = s.AddPokemonRegularTrainers;
        NUD_AddRegularCount.Value = Math.Max(1, Math.Min(6, s.AddPokemonRegularCount));

        CHK_ItemBossPk.Checked = s.AddHeldItemsBossTrainers;
        CHK_ItemImportantPk.Checked = s.AddHeldItemsImportantTrainers;
        CHK_ItemRegularPk.Checked = s.AddHeldItemsRegularTrainers;

        CHK_ForceFullyEvolved.Checked = s.ForceFullyEvolvedAtLevel;
        TB_ForceFullyEvolvedLevel.Value = Math.Max(30, Math.Min(65, s.ForceFullyEvolvedLevel));
        L_ForceFullyEvolvedVal.Text = TB_ForceFullyEvolvedLevel.Value.ToString();
        TB_TrainerLevelMod.Value = Math.Clamp(s.TrainerLevelModifierPercent, -100, 100);
        L_TrainerLevelModVal.Text = $"{TB_TrainerLevelMod.Value}%";

        RB_TotemUnchanged.Checked = s.TotemMode == 0;
        RB_TotemRandom.Checked = s.TotemMode == 1;
        RB_TotemRandomSimilar.Checked = s.TotemMode == 2;

        RB_AllyUnchanged.Checked = s.TotemAllyMode == 0;
        RB_AllyRandom.Checked = s.TotemAllyMode == 1;
        RB_AllyRandomSimilar.Checked = s.TotemAllyMode == 2;

        RB_AuraUnchanged.Checked = s.TotemAuraMode == 0;
        RB_AuraRandom.Checked = s.TotemAuraMode == 1;
        RB_AuraRandomSame.Checked = s.TotemAuraMode == 2;

        CHK_TotemHeldItems.Checked = s.TotemRandomizeHeldItems;
        CHK_TotemAllowAltFormes.Checked = s.TotemAllowAltFormes;
        TB_TotemLevelMod.Value = Math.Clamp(s.TotemLevelModifierPercent, -100, 100);
        L_TotemLevelModVal.Text = $"{TB_TotemLevelMod.Value}%";

        // Tab 5
        RB_WildUnchanged.Checked = s.WildPokemonMode == 0;
        RB_WildRandom.Checked = s.WildPokemonMode == 1;
        RB_WildArea1To1.Checked = s.WildPokemonMode == 2;
        RB_WildGlobal1To1.Checked = s.WildPokemonMode == 3;

        RB_WildRuleNone.Checked = s.WildAdditionalRule == 0;
        RB_WildRuleSimilar.Checked = s.WildAdditionalRule == 1;
        RB_WildRuleCatchEmAll.Checked = s.WildAdditionalRule == 2;
        RB_WildRuleTypeThemed.Checked = s.WildAdditionalRule == 3;

        CHK_WildTimeBased.Checked = s.WildUseTimeBased;
        CHK_WildNoLegendaries.Checked = s.WildDontUseLegendaries;
        CHK_WildMinCatchRate.Checked = s.WildSetMinCatchRate;
        TB_WildMinCatchRateVal.Value = Math.Max(1, Math.Min(5, s.WildMinCatchRate));
        L_WildMinCatchRateVal.Text = TB_WildMinCatchRateVal.Value.ToString();
        CHK_WildHeldItems.Checked = s.WildRandomizeHeldItems;
        CHK_WildBanBadItems.Checked = s.WildBanBadItems;
        CHK_WildShakingGrass.Checked = s.WildBalanceShakingGrass;
        TB_WildLevelMod.Value = Math.Clamp(s.WildLevelModifierPercent, -100, 100);
        L_WildLevelModVal.Text = $"{TB_WildLevelMod.Value}%";
        CHK_WildAllowAltFormes.Checked = s.WildAllowAltFormes;

        CB_WildReplacements.SelectedIndex = Math.Clamp(s.WildReplacementsPerSpecies, 0, CB_WildReplacements.Items.Count - 1);
        CHK_WildSplitByEncounterType.Checked = s.WildSplitByEncounterType;
        CHK_WildAvoidRepeats.Checked = s.WildAvoidRepeats;
        CHK_WildSplitByEncounterType.Enabled = CB_WildReplacements.SelectedIndex == Wild7Randomizer.ScopePerMap;
        RB_WildTypeNone.Checked = s.WildTypeRestriction == WildRestrictions.TypeNone;
        RB_WildTypeZoneThemes.Checked = s.WildTypeRestriction == WildRestrictions.TypeRandomZoneThemes;
        RB_WildTypeKeepPrimary.Checked = s.WildTypeRestriction == WildRestrictions.TypeKeepPrimary;
        CHK_WildKeepZoneThemes.Checked = s.WildKeepZoneThemes;
        CHK_WildKeepZoneThemes.Enabled = RB_WildTypeZoneThemes.Checked;
        RB_WildEvoNone.Checked = s.WildEvolutionRestriction == WildRestrictions.EvoNone;
        RB_WildEvoOnlyBasic.Checked = s.WildEvolutionRestriction == WildRestrictions.EvoOnlyBasic;
        RB_WildEvoSameStage.Checked = s.WildEvolutionRestriction == WildRestrictions.EvoSameStage;
        CHK_WildKeepEvoRelations.Checked = s.WildKeepEvolutionRelations;

        // Level Caps
        CHK_LevelCaps.Checked = s.EnableLevelCaps;
        CHK_CheapCandies.Checked = s.CheapRareCandies;
        int shiftIdx = Array.IndexOf(LevelCapShifts.All, s.LevelCapShift);
        CB_LevelCapStyle.SelectedIndex = s.LevelCapMatchTrainers
            ? LevelCapShifts.All.Length
            : shiftIdx >= 0 ? shiftIdx : LevelCapShifts.StandardIndex;
        NUD_LevelCapFinal.Value = Math.Clamp(s.LevelCapFinal, (int)NUD_LevelCapFinal.Minimum, (int)NUD_LevelCapFinal.Maximum);
        ApplySavedCaps(s.LevelCapCaps);

        // Tab 6
        RB_TMMovesUnchanged.Checked = s.TMHMMovesMode == 0;
        RB_TMMovesRandom.Checked = s.TMHMMovesMode == 1;
        RB_TMMovesNoGameBreaking.Checked = s.TMHMMovesMode == 2;
        CHK_KeepFieldTMs.Checked = s.KeepFieldMoveTMs;
        CHK_TMForceGoodMoves.Checked = s.TMForcePercentGoodMoves;
        TB_TMForceGoodMovesVal.Value = Math.Max(0, Math.Min(100, s.TMForcePercentGoodMovesValue));
        L_TMForceGoodMovesVal.Text = $"{TB_TMForceGoodMovesVal.Value}%";

        RB_TMCompUnchanged.Checked = s.TMHMCompatibilityMode == 0;
        RB_TMCompSameType.Checked = s.TMHMCompatibilityMode == 1;
        RB_TMCompCompletely.Checked = s.TMHMCompatibilityMode == 2;
        RB_TMCompFull.Checked = s.TMHMCompatibilityMode == 3;
        CHK_TMSanity.Checked = s.TMLevelupMoveSanity;
        CHK_TMFollowEvos.Checked = s.TMFollowEvolutions;
        CHK_FullHMComp.Checked = s.FullHMCompatibility;

        RB_TutorMovesUnchanged.Checked = s.MoveTutorMovesMode == 0;
        RB_TutorMovesRandom.Checked = s.MoveTutorMovesMode == 1;
        RB_TutorMovesNoGameBreaking.Checked = s.MoveTutorMovesMode == 2;
        CHK_KeepFieldTutors.Checked = s.KeepFieldMoveTutors;
        CHK_TutorForceGoodMoves.Checked = s.TutorForcePercentGoodMoves;
        TB_TutorForceGoodMovesVal.Value = Math.Max(0, Math.Min(100, s.TutorForcePercentGoodMovesValue));
        L_TutorForceGoodMovesVal.Text = $"{TB_TutorForceGoodMovesVal.Value}%";

        RB_TutorCompUnchanged.Checked = s.MoveTutorCompatibilityMode == 0;
        RB_TutorCompSameType.Checked = s.MoveTutorCompatibilityMode == 1;
        RB_TutorCompCompletely.Checked = s.MoveTutorCompatibilityMode == 2;
        RB_TutorCompFull.Checked = s.MoveTutorCompatibilityMode == 3;
        CHK_TutorSanity.Checked = s.TutorLevelupMoveSanity;
        CHK_TutorFollowEvos.Checked = s.TutorFollowEvolutions;

        // Tab 7
        RB_FieldItemsUnchanged.Checked = s.FieldItemsMode == 0;
        RB_FieldItemsShuffle.Checked = s.FieldItemsMode == 1;
        RB_FieldItemsRandom.Checked = s.FieldItemsMode == 2;
        RB_FieldItemsRandomEven.Checked = s.FieldItemsMode == 3;
        CHK_FieldItemsBanBad.Checked = s.FieldItemsBanBadItems;

        RB_ShopsUnchanged.Checked = s.SpecialShopsMode == 0;
        RB_ShopsShuffle.Checked = s.SpecialShopsMode == 1;
        RB_ShopsRandom.Checked = s.SpecialShopsMode == 2;
        CHK_ShopsRandomizeAll.Checked = s.RandomizeAllShops;
        CHK_ShopsBanBad.Checked = s.ShopBanBadItems;
        CHK_ShopsBanRegular.Checked = s.ShopBanRegularItems;
        CHK_ShopsBanOverpowered.Checked = s.ShopBanOverpoweredItems;
        CHK_ShopsBalancePrices.Checked = s.BalanceShopItemPrices;
        CHK_GuaranteeEvolutionItems.Checked = s.GuaranteeEvolutionItems;
        CHK_GuaranteeXItems.Checked = s.GuaranteeXItems;

        RB_PickupUnchanged.Checked = s.PickupItemsMode == 0;
        RB_PickupRandom.Checked = s.PickupItemsMode == 1;
        CHK_PickupBanBad.Checked = s.PickupBanBadItems;

        // Tab 8
        CHK_BanLuckyEgg.Checked = s.BanLuckyEgg;
        CHK_BalanceStaticPokemonLevels.Checked = s.BalanceStaticPokemonLevels;
        CHK_NoFreeLuckyEgg.Checked = s.NoFreeLuckyEgg;
        CHK_DontRevertTempAltFormes.Checked = s.DontRevertTempAltFormes;
        CHK_FastestText.Checked = s.FastestText;
        CHK_AllWildPokemonCanCallAllies.Checked = s.AllWildPokemonCanCallAllies;
        CHK_FastEggHatching.Checked = s.FastEggHatching;
        CHK_NoEVsFromPokemon.Checked = s.NoEVsFromPokemon;
        CHK_ExpMultiplier.Checked = s.CustomExpMultiplier;
        NUD_ExpMultiplier.Value = Math.Clamp(s.ExpMultiplier, 1, 255);
        ApplyPatchList(s.InstallPatches);
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Apply Universal Randomization according to all selected UPR-ZX settings?") != DialogResult.Yes)
            return;

        B_Randomize.Enabled = false;
        var settings = GetSettingsFromUI();
        var orchestrator = new UniversalRandomizer(Main.Config, settings, Main.RomFSPath);

        Action<SpeciesRandomizer, FormRandomizer> wildAction = null;
        if (settings.WildPokemonMode > 0 || settings.CompetitiveRandomizer)
        {
            wildAction = (rnd, form) =>
            {
                if (Main.Config.Generation == 7)
                {
                    var wild7 = new Wild7Randomizer
                    {
                        RandSpec = rnd,
                        RandForm = form,
                        WildPokemonMode = settings.WildPokemonMode > 0 ? settings.WildPokemonMode : 1,
                        TableRandomizationOption = 0,
                        LevelAmplifier = 1 + (settings.WildLevelModifierPercent / 100m),
                        ModifyLevel = settings.WildLevelModifierPercent != 0,
                        AllCanCallAllies = settings.AllWildPokemonCanCallAllies || settings.CompetitiveRandomizer,

                        ReplacementsPerSpecies = settings.WildReplacementsPerSpecies,
                        SplitByEncounterType = settings.WildSplitByEncounterType,
                        TypeRestriction = settings.WildTypeRestriction,
                        KeepZoneThemes = settings.WildKeepZoneThemes,
                        EvolutionRestriction = settings.WildEvolutionRestriction,
                        KeepEvolutionRelations = settings.WildKeepEvolutionRelations,
                        AvoidRepeats = settings.WildAvoidRepeats,
                        Config = Main.Config,
                    };
                    var encdata = Main.Config.GetlzGARCData("encdata");
                    var locationList = Main.Config.GetText(TextName.metlist_000000);
                    locationList = SMWE.GetGoodLocationList(locationList);
                    var zd = Main.Config.GetlzGARCData("zonedata");
                    var wd = Main.Config.GetlzGARCData("worlddata");

                    if (encdata != null && zd != null && wd != null)
                    {
                        var areas = Area7.GetArray(encdata, zd, wd, locationList);
                        var activeAreas = areas.Where(a => a.HasTables && a.Zones.Length > 0).OrderBy(a => a.Name).ToArray();
                        wild7.Execute(activeAreas, encdata);
                        encdata.Save();
                    }
                }
                else if (Main.Config.Generation == 6)
                {
                    var encdata = Main.Config.GetGARCData("encdata");
                    if (encdata?.Files != null)
                    {
                        for (int fileIdx = 0; fileIdx < encdata.Files.Length; fileIdx++)
                        {
                            byte[] ed = encdata.Files[fileIdx];
                            if (ed == null || ed.Length < 0x178) continue;
                            int offset = BitConverter.ToInt32(ed, 0x10) + 0x10;
                            if (offset < 0 || offset + 0x178 > ed.Length) continue;

                            for (int slotOfs = offset; slotOfs < offset + 0x178; slotOfs += 4)
                            {
                                ushort val = BitConverter.ToUInt16(ed, slotOfs);
                                ushort spec = (ushort)(val & 0x7FF);
                                if (spec == 0 || spec > Main.Config.MaxSpeciesID) continue;
                                ushort formVal = (ushort)(val >> 11);
                                ushort newSpec = (ushort)(settings.WildPokemonMode == 3 ? rnd.GetMappedSpecies(spec) : rnd.GetRandomSpecies(spec));
                                ushort newVal = (ushort)((formVal << 11) | (newSpec & 0x7FF));
                                BitConverter.GetBytes(newVal).CopyTo(ed, slotOfs);

                                if (settings.WildLevelModifierPercent != 0)
                                {
                                    byte minL = ed[slotOfs + 2];
                                    byte maxL = ed[slotOfs + 3];
                                    ed[slotOfs + 2] = (byte)Math.Clamp((int)(minL * (1 + settings.WildLevelModifierPercent / 100m)), 1, 100);
                                    ed[slotOfs + 3] = (byte)Math.Clamp((int)(maxL * (1 + settings.WildLevelModifierPercent / 100m)), 1, 100);
                                }
                            }
                        }
                        encdata.Save();
                    }
                }
            };
        }

        Action<SpeciesRandomizer, FormRandomizer> staticsAction = null;
        if (settings.StaticsMode > 0)
        {
            staticsAction = (rnd, form) =>
            {
                if (Main.Config.Generation == 7)
                {
                    var esg = Main.Config.GetGARCData("encounterstatic");
                    if (esg?.Files != null && esg.Files.Length > 1)
                    {
                        byte[] data = esg.Files[1]; // File 1: Encounters
                        if (data != null && data.Length >= EncounterStatic7.SIZE)
                        {
                            for (int i = 0; i < data.Length; i += EncounterStatic7.SIZE)
                            {
                                byte[] entry = new byte[EncounterStatic7.SIZE];
                                Array.Copy(data, i, entry, 0, entry.Length);
                                var enc = new EncounterStatic7(entry);
                                if (enc.Species > 0 && enc.Species <= Main.Config.MaxSpeciesID)
                                {
                                    ushort newSpec = (ushort)(settings.StaticsMode == 3 ? rnd.GetMappedSpecies(enc.Species) : rnd.GetRandomSpecies(enc.Species));
                                    enc.Species = newSpec;
                                    if (settings.StaticsLevelModifierPercent != 0)
                                    {
                                        enc.Level = (byte)Math.Clamp((int)(enc.Level * (1 + settings.StaticsLevelModifierPercent / 100m)), 1, 100);
                                    }
                                    Array.Copy(enc.Data, 0, data, i, EncounterStatic7.SIZE);
                                }
                            }
                            esg.Files[1] = data;
                            esg.Save();
                        }
                    }
                }
            };
        }

        try
        {
            orchestrator.Execute((msg, pct) =>
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => UpdateProgress(msg, pct)));
                }
                else
                {
                    UpdateProgress(msg, pct);
                }
            }, wildAction, staticsAction);

            WinFormsUtil.Alert("Universal Randomization finished successfully!\nAll game files have been updated with UPR-ZX parity.");
            this.Close();
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Universal Randomization failed:\n" + ex.Message, ex.StackTrace);
        }
        finally
        {
            B_Randomize.Enabled = true;
        }
    }

    private void OpenBSTLimitsDialog()
    {
        using var dialog = new Form
        {
            Text = "Base Stat Total (BST) Limits",
            Size = new Size(850, 360),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };
        WinFormsUtil.ApplyTheme(dialog);

        // --- Column 1: Minimum BST Floors ---
        var chkEnforceMin = new CheckBox { Text = "Enforce Minimum BST Floors", Location = new Point(15, 15), AutoSize = true, Checked = _enforceMinBST };

        var lbl31 = new Label { Text = "3-Stage (Stage 1):", Location = new Point(15, 45), AutoSize = true };
        var nud31 = new NumericUpDown { Location = new Point(180, 43), Width = 70, Minimum = 0, Maximum = 720, Value = _minBST3Stage1 };

        var lbl32 = new Label { Text = "3-Stage (Stage 2):", Location = new Point(15, 75), AutoSize = true };
        var nud32 = new NumericUpDown { Location = new Point(180, 73), Width = 70, Minimum = 0, Maximum = 720, Value = _minBST3Stage2 };

        var lbl33 = new Label { Text = "3-Stage (Stage 3):", Location = new Point(15, 105), AutoSize = true };
        var nud33 = new NumericUpDown { Location = new Point(180, 103), Width = 70, Minimum = 0, Maximum = 720, Value = _minBST3Stage3 };

        var lbl21 = new Label { Text = "2-Stage (Stage 1):", Location = new Point(15, 135), AutoSize = true };
        var nud21 = new NumericUpDown { Location = new Point(180, 133), Width = 70, Minimum = 0, Maximum = 720, Value = _minBST2Stage1 };

        var lbl22 = new Label { Text = "2-Stage (Stage 2):", Location = new Point(15, 165), AutoSize = true };
        var nud22 = new NumericUpDown { Location = new Point(180, 163), Width = 70, Minimum = 0, Maximum = 720, Value = _minBST2Stage2 };

        var lbl1 = new Label { Text = "1-Stage (No Evolution):", Location = new Point(15, 195), AutoSize = true };
        var nud1 = new NumericUpDown { Location = new Point(180, 193), Width = 70, Minimum = 0, Maximum = 720, Value = _minBST1Stage };

        var lblLeg = new Label { Text = "Legendaries / Mythicals:", Location = new Point(15, 225), AutoSize = true };
        var nudLeg = new NumericUpDown { Location = new Point(180, 223), Width = 70, Minimum = 0, Maximum = 720, Value = _minBSTLegendary };

        // --- Column 2: Maximum BST Ceilings ---
        var chkEnforceMax = new CheckBox { Text = "Enforce Maximum BST Ceilings", Location = new Point(290, 15), AutoSize = true, Checked = _enforceMaxBST };

        var mlbl31 = new Label { Text = "3-Stage (Stage 1):", Location = new Point(290, 45), AutoSize = true };
        var mnud31 = new NumericUpDown { Location = new Point(455, 43), Width = 70, Minimum = 0, Maximum = 999, Value = _maxBST3Stage1 };

        var mlbl32 = new Label { Text = "3-Stage (Stage 2):", Location = new Point(290, 75), AutoSize = true };
        var mnud32 = new NumericUpDown { Location = new Point(455, 73), Width = 70, Minimum = 0, Maximum = 999, Value = _maxBST3Stage2 };

        var mlbl33 = new Label { Text = "3-Stage (Stage 3):", Location = new Point(290, 105), AutoSize = true };
        var mnud33 = new NumericUpDown { Location = new Point(455, 103), Width = 70, Minimum = 0, Maximum = 999, Value = _maxBST3Stage3 };

        var mlbl21 = new Label { Text = "2-Stage (Stage 1):", Location = new Point(290, 135), AutoSize = true };
        var mnud21 = new NumericUpDown { Location = new Point(455, 133), Width = 70, Minimum = 0, Maximum = 999, Value = _maxBST2Stage1 };

        var mlbl22 = new Label { Text = "2-Stage (Stage 2):", Location = new Point(290, 165), AutoSize = true };
        var mnud22 = new NumericUpDown { Location = new Point(455, 163), Width = 70, Minimum = 0, Maximum = 999, Value = _maxBST2Stage2 };

        var mlbl1 = new Label { Text = "1-Stage (No Evolution):", Location = new Point(290, 195), AutoSize = true };
        var mnud1 = new NumericUpDown { Location = new Point(455, 193), Width = 70, Minimum = 0, Maximum = 999, Value = _maxBST1Stage };

        var mlblLeg = new Label { Text = "Legendaries / Mythicals:", Location = new Point(290, 225), AutoSize = true };
        var mnudLeg = new NumericUpDown { Location = new Point(455, 223), Width = 70, Minimum = 0, Maximum = 999, Value = _maxBSTLegendary };

        // --- Column 3: No Egregious Stats ---
        var chkNoEgregious = new CheckBox { Text = "No Egregious Stats", Location = new Point(565, 15), AutoSize = true, Checked = _noEgregiousStats };

        var elblSingle = new Label { Text = "Single Stat Cap:", Location = new Point(565, 45), AutoSize = true };
        var enudSingle = new NumericUpDown { Location = new Point(750, 43), Width = 70, Minimum = 2, Maximum = 255, Value = _noEgregiousStatsSingleCap };

        var elblRegular = new Label { Text = "Regular BST Cap:", Location = new Point(565, 75), AutoSize = true };
        var enudRegular = new NumericUpDown { Location = new Point(750, 73), Width = 70, Minimum = 0, Maximum = 999, Value = _noEgregiousStatsBSTCapRegular };

        var elblLegendary = new Label { Text = "Legendary BST Cap:", Location = new Point(565, 105), AutoSize = true };
        var enudLegendary = new NumericUpDown { Location = new Point(750, 103), Width = 70, Minimum = 0, Maximum = 999, Value = _noEgregiousStatsBSTCapLegendary };

        var chkAvoidMinmax = new CheckBox
        {
            Text = "Avoid Minmaxing",
            Location = new Point(565, 145),
            AutoSize = true,
            Checked = _avoidMinmaxing
        };
        var lblAvoidMinmax = new Label
        {
            Text = "Softens extreme spreads (e.g. 200/5)\nwithout changing total BST.",
            Location = new Point(565, 168),
            Size = new Size(240, 34),
            ForeColor = Color.Gray
        };

        var btnOk = new Button { Text = "OK", Location = new Point(380, 270), Size = new Size(90, 30), DialogResult = DialogResult.OK };

        dialog.Controls.AddRange(new Control[]
        {
            chkEnforceMin,
            lbl31, nud31, lbl32, nud32, lbl33, nud33,
            lbl21, nud21, lbl22, nud22,
            lbl1, nud1, lblLeg, nudLeg,

            chkEnforceMax,
            mlbl31, mnud31, mlbl32, mnud32, mlbl33, mnud33,
            mlbl21, mnud21, mlbl22, mnud22,
            mlbl1, mnud1, mlblLeg, mnudLeg,

            chkNoEgregious,
            elblSingle, enudSingle, elblRegular, enudRegular, elblLegendary, enudLegendary,
            chkAvoidMinmax, lblAvoidMinmax,

            btnOk
        });
        dialog.AcceptButton = btnOk;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _enforceMinBST = chkEnforceMin.Checked;
            _minBST3Stage1 = (int)nud31.Value;
            _minBST3Stage2 = (int)nud32.Value;
            _minBST3Stage3 = (int)nud33.Value;
            _minBST2Stage1 = (int)nud21.Value;
            _minBST2Stage2 = (int)nud22.Value;
            _minBST1Stage = (int)nud1.Value;
            _minBSTLegendary = (int)nudLeg.Value;

            _enforceMaxBST = chkEnforceMax.Checked;
            _maxBST3Stage1 = (int)mnud31.Value;
            _maxBST3Stage2 = (int)mnud32.Value;
            _maxBST3Stage3 = (int)mnud33.Value;
            _maxBST2Stage1 = (int)mnud21.Value;
            _maxBST2Stage2 = (int)mnud22.Value;
            _maxBST1Stage = (int)mnud1.Value;
            _maxBSTLegendary = (int)mnudLeg.Value;

            _noEgregiousStats = chkNoEgregious.Checked;
            _noEgregiousStatsSingleCap = (int)enudSingle.Value;
            _noEgregiousStatsBSTCapRegular = (int)enudRegular.Value;
            _noEgregiousStatsBSTCapLegendary = (int)enudLegendary.Value;

            _avoidMinmaxing = chkAvoidMinmax.Checked;
        }
    }

    private void UpdateProgress(string msg, int pct)
    {
        L_Status.Text = msg;
        PB_Progress.Value = Math.Max(0, Math.Min(100, pct));
        Application.DoEvents();
    }

    /// <summary>
    /// The categories, plus a searchable list of every ability the loaded ROM actually has.
    /// </summary>
    private void OpenBanAbilitiesDialog()
    {
        using var dlg = new Form
        {
            Text = "Ban Abilities",
            Width = 520,
            Height = 560,
            FormBorderStyle = FormBorderStyle.Sizable,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(30, 30, 40)
        };

        var chkWonder = new CheckBox { Text = "Allow Wonder Guard", Location = new Point(16, 14), AutoSize = true, Checked = CHK_AbilAllowWonderGuard.Checked, ForeColor = Color.White };
        var chkTrapping = new CheckBox { Text = "Ban trapping (Shadow Tag, Arena Trap, Magnet Pull)", Location = new Point(16, 40), AutoSize = true, Checked = CHK_AbilTrapping.Checked, ForeColor = Color.White };
        var chkNegative = new CheckBox { Text = "Ban negative (Truant, Slow Start, Defeatist, …)", Location = new Point(16, 66), AutoSize = true, Checked = CHK_AbilNegative.Checked, ForeColor = Color.White };
        var chkBad = new CheckBox { Text = "Ban do-nothing (Illuminate, Run Away, Honey Gather, …)", Location = new Point(16, 92), AutoSize = true, Checked = CHK_AbilBad.Checked, ForeColor = Color.White };

        var lblList = new Label { Text = "Ban individually:", Location = new Point(16, 124), AutoSize = true, ForeColor = Color.White };
        var search = new TextBox { Location = new Point(120, 121), Width = 240 };
        var lblCount = new Label { Location = new Point(370, 124), AutoSize = true, ForeColor = Color.Gray };

        var list = new CheckedListBox
        {
            Location = new Point(16, 150),
            Size = new Size(470, 320),
            CheckOnClick = true,
            BackColor = Color.FromArgb(45, 45, 58),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        // Names from the loaded ROM, so an Expansion Pack build offers its extra abilities too.
        string[] abilityNames = Main.Config?.GetText(TextName.AbilityNames) ?? [];
        var allNames = abilityNames
            .Skip(1)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != "—" && n != "———")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var banned = new HashSet<string>(_bannedAbilityNames, StringComparer.OrdinalIgnoreCase);

        void Repopulate()
        {
            string q = search.Text.Trim();
            var shown = allNames.Where(n => q.Length == 0 || n.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            list.BeginUpdate();
            list.Items.Clear();
            foreach (string n in shown) list.Items.Add(n, banned.Contains(n));
            list.EndUpdate();
            lblCount.Text = $"{banned.Count} banned";
        }

        // Keep the set in step as boxes are ticked, so filtering never loses a choice: rebuilding
        // the list on every keystroke would otherwise discard whatever was ticked but scrolled away.
        list.ItemCheck += (_, e) =>
        {
            string name = list.Items[e.Index]?.ToString();
            if (string.IsNullOrEmpty(name)) return;
            if (e.NewValue == CheckState.Checked) banned.Add(name); else banned.Remove(name);
            BeginInvoke(() => lblCount.Text = $"{banned.Count} banned");
        };
        search.TextChanged += (_, _) => Repopulate();

        // Ticking a category shows what it covers rather than leaving the label to be trusted.
        void ApplyCategory(CheckBox box, string[] names)
        {
            box.CheckedChanged += (_, _) =>
            {
                foreach (string n in names)
                {
                    if (box.Checked) banned.Add(n); else banned.Remove(n);
                }
                Repopulate();
            };
        }
        ApplyCategory(chkTrapping, AbilityBanList.TrappingAbilities);
        ApplyCategory(chkNegative, AbilityBanList.NegativeAbilities);
        ApplyCategory(chkBad, AbilityBanList.BadAbilities);

        var btnClear = new Button { Text = "Clear all", Location = new Point(16, 482), Width = 90, Height = 28, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        btnClear.Click += (_, _) =>
        {
            banned.Clear();
            chkTrapping.Checked = chkNegative.Checked = chkBad.Checked = false;
            Repopulate();
        };

        var btnOk = new Button { Text = "OK", Location = new Point(300, 482), Width = 90, Height = 28, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Teal, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(396, 482), Width = 90, Height = 28, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };

        dlg.Controls.AddRange([chkWonder, chkTrapping, chkNegative, chkBad, lblList, search, lblCount, list, btnClear, btnOk, btnCancel]);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;
        Repopulate();

        if (dlg.ShowDialog() != DialogResult.OK) return;

        CHK_AbilAllowWonderGuard.Checked = chkWonder.Checked;
        CHK_AbilTrapping.Checked = chkTrapping.Checked;
        CHK_AbilNegative.Checked = chkNegative.Checked;
        CHK_AbilBad.Checked = chkBad.Checked;
        _bannedAbilityNames = [.. banned];
    }

    /// <summary>Abilities banned one by one, on top of the category toggles.</summary>
    private List<string> _bannedAbilityNames = [];

    private void OpenLimitPokemonDialog()
    {
        using var dlg = new Form
        {
            Text = "Limit Pokemon Generations & Pool",
            Width = 360,
            Height = 320,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(30, 30, 40)
        };

        var chkG1 = new CheckBox { Text = "Generation 1", Location = new Point(20, 20), AutoSize = true, Checked = _limitGen1, ForeColor = Color.White };
        var chkG2 = new CheckBox { Text = "Generation 2", Location = new Point(20, 45), AutoSize = true, Checked = _limitGen2, ForeColor = Color.White };
        var chkG3 = new CheckBox { Text = "Generation 3", Location = new Point(20, 70), AutoSize = true, Checked = _limitGen3, ForeColor = Color.White };
        var chkG4 = new CheckBox { Text = "Generation 4", Location = new Point(20, 95), AutoSize = true, Checked = _limitGen4, ForeColor = Color.White };
        var chkG5 = new CheckBox { Text = "Generation 5", Location = new Point(20, 120), AutoSize = true, Checked = _limitGen5, ForeColor = Color.White };
        var chkG6 = new CheckBox { Text = "Generation 6", Location = new Point(20, 145), AutoSize = true, Checked = _limitGen6, ForeColor = Color.White };
        var chkG7 = new CheckBox { Text = "Generation 7", Location = new Point(20, 170), AutoSize = true, Checked = _limitGen7, ForeColor = Color.White };
        var chkG89 = new CheckBox { Text = "Gen 8/9 (Expansion)", Location = new Point(20, 195), AutoSize = true, Checked = _limitGen8 && _limitGen9, ForeColor = Color.White };

        var chkL = new CheckBox { Text = "Allow Legendaries", Location = new Point(180, 20), AutoSize = true, Checked = _limitAllowLegendaries, ForeColor = Color.White };
        var chkE = new CheckBox { Text = "Allow Mythicals", Location = new Point(180, 45), AutoSize = true, Checked = _limitAllowMythicals, ForeColor = Color.White };

        var btnOk = new Button { Text = "OK", Location = new Point(130, 235), Width = 90, Height = 28, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.Teal };

        dlg.Controls.AddRange(new Control[] { chkG1, chkG2, chkG3, chkG4, chkG5, chkG6, chkG7, chkG89, chkL, chkE, btnOk });
        dlg.AcceptButton = btnOk;

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _limitGen1 = chkG1.Checked;
            _limitGen2 = chkG2.Checked;
            _limitGen3 = chkG3.Checked;
            _limitGen4 = chkG4.Checked;
            _limitGen5 = chkG5.Checked;
            _limitGen6 = chkG6.Checked;
            _limitGen7 = chkG7.Checked;
            _limitGen8 = chkG89.Checked;
            _limitGen9 = chkG89.Checked;
            _limitAllowLegendaries = chkL.Checked;
            _limitAllowMythicals = chkE.Checked;
        }
    }
}
