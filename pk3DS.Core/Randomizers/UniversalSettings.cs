using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;

namespace pk3DS.Core.Randomizers;

public class UniversalSettings
{
    // Seed
    public string Seed { get; set; } = "";

    // General Options
    public bool LimitPokemon { get; set; } = false;
    public bool CompetitiveRandomizer { get; set; } = false;
    public bool NoIrregularAltFormes { get; set; } = false;
    public bool RaceMode { get; set; } = false;

    // Generations / Pool (Expansion Pack compatible!)
    public bool Gen1 { get; set; } = true;
    public bool Gen2 { get; set; } = true;
    public bool Gen3 { get; set; } = true;
    public bool Gen4 { get; set; } = true;
    public bool Gen5 { get; set; } = true;
    public bool Gen6 { get; set; } = true;
    public bool Gen7 { get; set; } = true;
    public bool Gen8 { get; set; } = true; // Expansion species 808-905
    public bool Gen9 { get; set; } = true; // Expansion species 906-1025
    public bool AllowLegendaries { get; set; } = true;
    public bool AllowMythicals { get; set; } = true;
    public bool AllowShedinja { get; set; } = false;

    // Filters
    public bool MatchBST { get; set; } = true;
    public bool MatchType { get; set; } = false;
    public bool MatchEXP { get; set; } = false;

    // TAB 1: POKEMON TRAITS
    // Base Statistics
    public int BaseStatsMode { get; set; } = 0; // 0: Unchanged, 1: Shuffle, 2: Random
    public int StatVariancePercent { get; set; } = 25;
    public bool BaseStatsFollowEvolutions { get; set; } = false;
    public bool BaseStatsFollowMegas { get; set; } = false;
    public bool RandomizeAddedStatsOnEvo { get; set; } = false;
    public bool UpdateBaseStatsToGen { get; set; } = false;
    public int UpdateBaseStatsGenIndex { get; set; } = 6;
    public bool StandardizeExpCurves { get; set; } = false;
    public int StandardizeExpCurveTarget { get; set; } = 0; // 0: Medium Fast
    public int ExpCurvePokemonScope { get; set; } = 0; // 0: Legendaries: Slow, 1: Strong Legendaries: Slow, 2: All Pokemon

    // Minimum BST Floors
    public bool EnforceMinimumBST { get; set; } = true;
    public int MinBST3Stage1 { get; set; } = 300;
    public int MinBST3Stage2 { get; set; } = 450;
    public int MinBST3Stage3 { get; set; } = 600;
    public int MinBST2Stage1 { get; set; } = 350;
    public int MinBST2Stage2 { get; set; } = 550;
    public int MinBST1Stage { get; set; } = 500;
    public int MinBSTLegendary { get; set; } = 600;

    // Maximum BST Ceilings (mirrors Minimum BST Floors)
    public bool EnforceMaximumBST { get; set; } = false;
    public int MaxBST3Stage1 { get; set; } = 500;
    public int MaxBST3Stage2 { get; set; } = 600;
    public int MaxBST3Stage3 { get; set; } = 700;
    public int MaxBST2Stage1 { get; set; } = 550;
    public int MaxBST2Stage2 { get; set; } = 650;
    public int MaxBST1Stage { get; set; } = 600;
    public int MaxBSTLegendary { get; set; } = 780;

    // No Egregious Stats — caps any single base stat and mellows out overall BST
    public bool NoEgregiousStats { get; set; } = false;
    public int NoEgregiousStatsSingleCap { get; set; } = 200;
    public int NoEgregiousStatsBSTCapRegular { get; set; } = 600;
    public int NoEgregiousStatsBSTCapLegendary { get; set; } = 780;

    // Avoid Minmaxing — softens extreme per-stat spreads while preserving each Pokemon's BST
    public bool AvoidMinmaxing { get; set; } = false;

    // Trainer team gimmick toggles
    public bool NoMegaEvolution { get; set; } = false;
    public bool NoZMoves { get; set; } = false;
    public bool TrainerFullIVs { get; set; } = true;

    // Pokemon Types
    public int TypesMode { get; set; } = 0; // 0: Unchanged, 1: Random (follow evos), 2: Random (completely)
    public bool TypesFollowMegas { get; set; } = false;
    public bool ForceDualTypes { get; set; } = false;

    /// <summary>
    /// How the 18x18 type effectiveness chart in code.bin is rewritten.
    /// </summary>
    public int TypeEffectivenessMode { get; set; } = 0;

    /// <summary>Inverse only: keep as many immunities as the original chart had.</summary>
    public bool TypeEffectivenessAddRandomImmunities { get; set; } = false;

    // Pokemon Abilities
    public int AbilitiesMode { get; set; } = 0; // 0: Unchanged, 1: Random
    public bool AllowWonderGuard { get; set; } = true;
    public bool AbilitiesFollowEvolutions { get; set; } = true;
    public bool AbilitiesFollowMegas { get; set; } = true;
    public bool BanTrappingAbilities { get; set; } = false;
    public bool CombineDuplicateAbilities { get; set; } = false;
    public bool BanNegativeAbilities { get; set; } = true;
    public bool BanBadAbilities { get; set; } = false;
    public bool EnsureTwoAbilities { get; set; } = false;

    /// <summary>
    /// Individual abilities the user banned by name, on top of the category toggles above.
    /// </summary>
    public List<string> BannedAbilityNames { get; set; } = [];

    // Pokemon Evolutions
    public int EvolutionsMode { get; set; } = 0; // 0: Unchanged, 1: Random, 2: Random Every Level
    public bool EvosSimilarStrength { get; set; } = true;
    public bool EvosSameTyping { get; set; } = false;
    public bool LimitEvosTo3Stages { get; set; } = true;
    public bool EvosForceChange { get; set; } = false;
    public bool EvosAllowAltFormes { get; set; } = true;
    public bool ChangeImpossibleEvos { get; set; } = true;
    public bool MakeEvosEasier { get; set; } = false;
    public bool RemoveTimeBasedEvos { get; set; } = false;

    // TAB 2: STARTERS, STATICS & TRADES
    // Starters
    public int StartersMode { get; set; } = 0; // 0: Unchanged, 1: Custom, 2: Random (completely), 3: Random (basic with 2 evos)

    /// <summary>
    /// Type shape imposed on the three starters.
    /// </summary>
    public int StarterTypeRestriction { get; set; } = 0;

    /// <summary>Type index used when <see cref="StarterTypeRestriction"/> is Single Type.</summary>
    public int StarterSingleType { get; set; } = 0;

    /// <summary>Require every starter to have exactly one type.</summary>
    public bool StarterNoDualTypes { get; set; } = false;
    public int CustomStarter1 { get; set; } = 1;
    public int CustomStarter2 { get; set; } = 4;
    public int CustomStarter3 { get; set; } = 7;
    public bool RandomizeStarterHeldItems { get; set; } = false;
    public bool StartersBanBadItems { get; set; } = true;
    public bool StartersAllowAltFormes { get; set; } = true;

    // Statics
    public int StaticsMode { get; set; } = 0; // 0: Unchanged, 1: Swap Legendaries/Standards, 2: Random (completely), 3: Random (similar strength)
    public bool StaticsRandomize600BST { get; set; } = false;
    public bool LimitMainGameLegendaries { get; set; } = true;
    public bool StaticsAllowAltFormes { get; set; } = true;
    public bool StaticsSwapMegaEvolvables { get; set; } = false;
    public bool StaticsFixMusic { get; set; } = false;
    public int StaticsLevelModifierPercent { get; set; } = 0;

    // Trades
    public int TradesMode { get; set; } = 0; // 0: Unchanged, 1: Randomize Given Only, 2: Randomize Both
    public bool TradesRandomizeNicknames { get; set; } = false;
    public bool TradesRandomizeOTs { get; set; } = false;
    public bool TradesRandomizeIVs { get; set; } = false;
    public bool TradesRandomizeItems { get; set; } = false;

    // TAB 3: MOVES & MOVESETS
    // Move Data
    public bool RandomizeMovePower { get; set; } = false;
    public bool RandomizeMoveAccuracy { get; set; } = false;
    public bool RandomizeMovePP { get; set; } = false;
    public bool RandomizeMoveTypes { get; set; } = false;
    public bool RandomizeMoveCategory { get; set; } = false;
    public bool UpdateMovesToGen { get; set; } = false;
    public int UpdateMovesGenIndex { get; set; } = 6;

    // Pokemon Movesets
    public int MovesetsMode { get; set; } = 0; // 0: Unchanged, 1: Random (prefer same type), 2: Random (completely), 3: Metronome Only
    public bool GuaranteedLevel1Moves { get; set; } = false;
    public int GuaranteedLevel1MovesCount { get; set; } = 4;
    public bool ReorderDamagingMoves { get; set; } = true;
    public bool NoGameBreakingMoves { get; set; } = true;
    public bool ForceGoodDamagingMoves { get; set; } = false;
    public int ForceGoodDamagingMovesPercent { get; set; } = 40;
    public bool EvolutionMovesForAll { get; set; } = false;

    // TAB 4: FOE POKEMON
    // Trainer Pokemon
    /// <summary>
    /// How trainer teams are re-picked.
    /// </summary>
    public int TrainerPokemonMode { get; set; } = 0;

    /// <summary>Try not to give one trainer the same species twice.</summary>
    public bool TrainerAvoidDuplicates { get; set; } = false;

    /// <summary>Push boss teams (Kahunas, Elite Four, Guzma, Lusamine) toward covering different types.</summary>
    public bool TrainerDiverseTypesBoss { get; set; } = false;

    /// <summary>Push important trainers' teams toward covering different types.</summary>
    public bool TrainerDiverseTypesImportant { get; set; } = false;

    /// <summary>Push every other trainer's team toward covering different types.</summary>
    public bool TrainerDiverseTypesRegular { get; set; } = false;

    /// <summary>
    /// What to do with each trainer's battle format.
    /// </summary>
    public int BattleStyleMode { get; set; } = 0;

    /// <summary>
    /// Format used when <see cref="BattleStyleMode"/> is Single Style: 0 Singles, 1 Doubles.
    /// </summary>
    public int BattleStyleChoice { get; set; } = 0;
    public int MasterAIMode { get; set; } = 0; // 0: Normal, 1: Important Trainers Only, 2: All Trainers (Master AI)
    public bool DoubleBattleMode { get; set; } = false;
    public bool BetterTrainerMovesets { get; set; } = true;
    public bool RivalCarriesStarter { get; set; } = true;
    public bool TrainerTrySimilarStrength { get; set; } = true;
    public bool WeightTypesByNumPokemon { get; set; } = false;
    public bool TrainerDontUseLegendaries { get; set; } = true;
    public bool NoEarlyWonderGuard { get; set; } = true;
    public bool TrainerAllowAltFormes { get; set; } = true;
    public bool TrainerSwapMegaEvolvables { get; set; } = false;
    public bool TrainerRandomShiny { get; set; } = false;
    public bool RandomizeTrainerNames { get; set; } = false;
    public bool RandomizeTrainerClassNames { get; set; } = false;

    public bool AddPokemonBossTrainers { get; set; } = false;
    public int AddPokemonBossCount { get; set; } = 1;
    public bool AddPokemonImportantTrainers { get; set; } = false;
    public int AddPokemonImportantCount { get; set; } = 1;
    public bool AddPokemonRegularTrainers { get; set; } = false;
    public int AddPokemonRegularCount { get; set; } = 1;

    public bool AddHeldItemsBossTrainers { get; set; } = false;
    public bool AddHeldItemsImportantTrainers { get; set; } = false;
    public bool AddHeldItemsRegularTrainers { get; set; } = false;
    public bool HeldItemsConsumableOnly { get; set; } = false;
    public bool HeldItemsSensibleOnly { get; set; } = true;
    public bool HeldItemsHighestLevelOnly { get; set; } = false;

    public bool ForceFullyEvolvedAtLevel { get; set; } = false;
    public int ForceFullyEvolvedLevel { get; set; } = 36;
    public int TrainerLevelModifierPercent { get; set; } = 0;
    public bool PokemonLeagueUniqueTeams { get; set; } = false;
    public int PokemonLeagueUniqueTeamsCount { get; set; } = 1;

    // Totem Pokemon (Gen 7)
    public int TotemMode { get; set; } = 0; // 0: Unchanged, 1: Random, 2: Random (similar strength)
    public int TotemAllyMode { get; set; } = 0; // 0: Unchanged, 1: Random, 2: Random (similar strength)
    public int TotemAuraMode { get; set; } = 0; // 0: Unchanged, 1: Random, 2: Random (same strength)
    public bool TotemRandomizeHeldItems { get; set; } = false;
    public bool TotemAllowAltFormes { get; set; } = true;
    public int TotemLevelModifierPercent { get; set; } = 0;

    // TAB 5: WILD POKEMON
    public int WildPokemonMode { get; set; } = 0; // 0: Unchanged, 1: Random, 2: Area 1-to-1, 3: Global 1-to-1
    public int WildAdditionalRule { get; set; } = 0; // 0: None, 1: Similar Strength, 2: Catch Em All Mode, 3: Type Themed Areas
    public bool WildUseTimeBased { get; set; } = false;
    public bool WildDontUseLegendaries { get; set; } = true;
    public bool WildSetMinCatchRate { get; set; } = false;
    public int WildMinCatchRate { get; set; } = 3;
    public bool WildRandomizeHeldItems { get; set; } = false;
    public bool WildBanBadItems { get; set; } = true;
    public bool WildBalanceShakingGrass { get; set; } = false;
    public int WildLevelModifierPercent { get; set; } = 0;
    public bool WildAllowAltFormes { get; set; } = true;

    /// <summary>
    /// How widely one original species may be replaced by different things.
    /// </summary>
    public int WildReplacementsPerSpecies { get; set; } = 0;

    /// <summary>Split an encounter set by its encounter type before mapping (One Per Map only).</summary>
    public bool WildSplitByEncounterType { get; set; } = false;

    /// <summary>
    /// Type shape imposed on wild encounters.
    /// </summary>
    public int WildTypeRestriction { get; set; } = 0;

    /// <summary>Keep a themed zone's theme rather than rolling a new one (Random Zone Themes only).</summary>
    public bool WildKeepZoneThemes { get; set; } = false;

    /// <summary>
    /// Evolution shape imposed on wild encounters.
    /// </summary>
    public int WildEvolutionRestriction { get; set; } = 0;

    /// <summary>Map a family's members onto one replacement family, keeping their relations.</summary>
    public bool WildKeepEvolutionRelations { get; set; } = false;

    /// <summary>
    /// Try not to put the same species in one encounter table twice.
    /// </summary>
    public bool WildAvoidRepeats { get; set; } = false;

    // TAB 6: TM/HMS & TUTORS
    // TMs & HMs
    /// <summary>
    /// Drop Rare Candy to 10 PokéDollars and stock it in every ordinary Poké Mart.
    /// </summary>
    public bool CheapRareCandies { get; set; } = false;

    /// <summary>Price in PokéDollars used by <see cref="CheapRareCandies"/>.</summary>
    public int CheapRareCandyPrice { get; set; } = 10;

    // TAB: LEVEL CAPS
    /// <summary>Install the story-flag level caps as part of a randomize.</summary>
    public bool EnableLevelCaps { get; set; } = false;

    /// <summary>
    /// Levels added to (or taken off) every checkpoint, relative to the researched curve.
    /// </summary>
    public int LevelCapShift { get; set; } = 0;

    /// <summary>
    /// Read the curve's shape from the ROM's own trainer levels instead of the researched values.
    /// </summary>
    public bool LevelCapMatchTrainers { get; set; } = false;

    /// <summary>Cap at the final checkpoint; the whole curve is scaled to land here.</summary>
    public int LevelCapFinal { get; set; } = 100;

    /// <summary>
    /// One cap per checkpoint, in table order, as edited on the Level Caps tab.
    /// </summary>
    public List<int> LevelCapCaps { get; set; } = [];

    /// <summary>
    /// Expand the TM table to 128 slots. Off by default, and deliberately not on the randomizer UI.
    /// </summary>
    public bool ExpandTMs { get; set; } = false;

    public int TMHMMovesMode { get; set; } = 0; // 0: Unchanged, 1: Random, 2: No Game-Breaking Moves
    public bool KeepFieldMoveTMs { get; set; } = true;
    public bool TMForcePercentGoodMoves { get; set; } = false;
    public int TMForcePercentGoodMovesValue { get; set; } = 40;

    public int TMHMCompatibilityMode { get; set; } = 0; // 0: Unchanged, 1: Random (prefer same type), 2: Random (completely), 3: Full Compatibility
    public bool TMLevelupMoveSanity { get; set; } = true;
    public bool TMFollowEvolutions { get; set; } = true;
    public bool FullHMCompatibility { get; set; } = false;

    // Move Tutors
    public int MoveTutorMovesMode { get; set; } = 0; // 0: Unchanged, 1: Random, 2: No Game-Breaking Moves
    public bool KeepFieldMoveTutors { get; set; } = true;
    public bool TutorForcePercentGoodMoves { get; set; } = false;
    public int TutorForcePercentGoodMovesValue { get; set; } = 40;

    public int MoveTutorCompatibilityMode { get; set; } = 0; // 0: Unchanged, 1: Random (prefer same type), 2: Random (completely), 3: Full Compatibility
    public bool TutorLevelupMoveSanity { get; set; } = true;
    public bool TutorFollowEvolutions { get; set; } = true;

    // TAB 7: ITEMS
    // Field Items
    public int FieldItemsMode { get; set; } = 0; // 0: Unchanged, 1: Shuffle, 2: Random, 3: Random (even distribution)
    public bool FieldItemsBanBadItems { get; set; } = true;

    // Special Shops
    public int SpecialShopsMode { get; set; } = 0; // 0: Unchanged, 1: Shuffle, 2: Random
    public bool ShopBanBadItems { get; set; } = true;
    public bool ShopBanRegularItems { get; set; } = false;
    public bool ShopBanOverpoweredItems { get; set; } = false;
    public bool BalanceShopItemPrices { get; set; } = false;
    public bool GuaranteeEvolutionItems { get; set; } = false;
    public bool GuaranteeXItems { get; set; } = false;
    public bool RandomizeAllShops { get; set; } = false;

    // Pickup Items
    public int PickupItemsMode { get; set; } = 0; // 0: Unchanged, 1: Random
    public bool PickupBanBadItems { get; set; } = true;

    // TAB 8: MISC. TWEAKS
    public bool BWExpPatch { get; set; } = false;
    public bool RunningShoesIndoors { get; set; } = false;
    public bool UpdateTypeEffectiveness { get; set; } = false;
    public bool BanLuckyEgg { get; set; } = false;
    public bool BalanceStaticPokemonLevels { get; set; } = false;
    public bool FastDistortionWorld { get; set; } = false;
    public bool NerfXAccuracy { get; set; } = false;
    public bool RandomizePCPotion { get; set; } = false;
    public bool ForceChallengeMode { get; set; } = false;
    public bool NoFreeLuckyEgg { get; set; } = false;
    public bool DontRevertTempAltFormes { get; set; } = false;
    public bool UpdateRotomApplianceTypings { get; set; } = false;
    public bool FixCritRate { get; set; } = false;
    public bool AllowPikachuEvolution { get; set; } = false;
    public bool LowerCasePokemonNames { get; set; } = false;
    public bool BanBigMoneyManiacItems { get; set; } = false;
    public bool RunWithoutRunningShoes { get; set; } = false;
    public bool DisableLowHPMusic { get; set; } = false;
    public bool FastestText { get; set; } = false;
    public bool GiveNationalDexAtStart { get; set; } = false;
    public bool RandomizeCatchingTutorial { get; set; } = false;
    public bool AllWildPokemonCanCallAllies { get; set; } = false;
    public bool FasterHPAndExpBars { get; set; } = false;

    /// <summary>
    /// Drop every species' egg hatch counter to its minimum.
    /// </summary>
    public bool FastEggHatching { get; set; } = false;

    /// <summary>
    /// Zero every species' EV yield, so nothing trains anything by being defeated.
    /// </summary>
    public bool NoEVsFromPokemon { get; set; } = false;

    /// <summary>
    /// Multiplier to scale battle EXP yield (1 to 255).
    /// </summary>
    public bool CustomExpMultiplier { get; set; } = false;
    public int ExpMultiplier { get; set; } = 2;

    /// <summary>
    /// Research Center recipes to install as part of a randomize, by name.
    /// </summary>
    public List<string> InstallPatches { get; set; } = [];

    // Preset Encoding & Decoding
    public string ExportSettingsString()
    {
        try
        {
            string json = JsonSerializer.Serialize(this);
            return "UPRZX3DS_" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
        catch { return ""; }
    }

    public static UniversalSettings ImportSettingsString(string encoded)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(encoded)) return new UniversalSettings();
            string raw = encoded.Trim();
            if (raw.StartsWith("UPRZX3DS_")) raw = raw["UPRZX3DS_".Length..];
            byte[] bytes = Convert.FromBase64String(raw);
            string json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<UniversalSettings>(json) ?? new UniversalSettings();
        }
        catch { return new UniversalSettings(); }
    }
}
