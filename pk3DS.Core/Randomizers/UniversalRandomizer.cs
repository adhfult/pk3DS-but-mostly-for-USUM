using System;
using System.Collections.Generic;
using System.IO;
using pk3DS.Core.Structures;
using pk3DS.Core.Structures.PersonalInfo;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace pk3DS.Core.Randomizers;

public class UniversalRandomizer
{
    private readonly GameConfig Config;
    private readonly UniversalSettings Settings;
    public string RomFSPath { get; set; }

    /// <summary>
    /// Abilities the user banned, resolved once against this ROM's ability text.
    /// </summary>
    private AbilityBanList AbilityBans { get; set; }

    public UniversalRandomizer(GameConfig config, UniversalSettings settings, string romfsPath = "")
    {
        Config = config;
        Settings = settings;
        RomFSPath = romfsPath;
        AbilityBans = new AbilityBanList(config?.GetText(TextName.AbilityNames), settings);
    }

    public void Execute(Action<string, int> progressCallback = null, Action<SpeciesRandomizer, FormRandomizer> wildRandomizerAction = null, Action<SpeciesRandomizer, FormRandomizer> staticsRandomizerAction = null)
    {
        // 1. Initialize Seed
        int seed = GenerateSeed(Settings.Seed);
        Util.ReseedRand(seed);
        progressCallback?.Invoke("Initializing RNG seed and pool filters...", 5);

        bool applyExpansionEvos = Config.MaxSpeciesID >= 1025;
        if (applyExpansionEvos)
        {
            int added = ExpansionPackEvolutions.ApplyAndSave(Config);
            if (added > 0)
                progressCallback?.Invoke($"Added {added} missing Expansion Pack evolution method(s).", 6);
        }

        // 2. Setup Species Pool (Supports Expansion Pack 808-1025!)
        var speciesRand = new SpeciesRandomizer(Config)
        {
            G1 = Settings.Gen1,
            G2 = Settings.Gen2,
            G3 = Settings.Gen3,
            G4 = Settings.Gen4,
            G5 = Settings.Gen5,
            G6 = Settings.Gen6,
            G7 = Settings.Gen7,
            G8 = Settings.Gen8,
            G9 = Settings.Gen9,

            L = Settings.AllowLegendaries,
            E = Settings.AllowMythicals,
            Shedinja = Settings.AllowShedinja,

            rBST = Settings.MatchBST,
            rType = Settings.MatchType,
            rEXP = Settings.MatchEXP
        };
        speciesRand.Initialize();
        speciesRand.GenerateGlobalMapping(Config.Evolutions);

        // 3. Personal Data (Base Stats, Abilities, Types, Catch Rates, Held Items, Egg Groups)
        if (Settings.BaseStatsMode > 0 || Settings.AbilitiesMode > 0 || Settings.TypesMode > 0 ||
            Settings.WildSetMinCatchRate || Settings.FieldItemsBanBadItems || Settings.TotemRandomizeHeldItems ||
            Settings.EnforceMaximumBST || Settings.NoEgregiousStats ||
            Settings.FastEggHatching || Settings.NoEVsFromPokemon)
        {
            progressCallback?.Invoke("Randomizing Pokémon Traits (Base Stats, Abilities, Types)...", 20);

            var pRandomizer = new PersonalRandomizer(Config.Personal.Table, Config)
            {
                ModifyStats = Settings.BaseStatsMode == 2,
                ShuffleStats = Settings.BaseStatsMode == 1,
                PreserveBST = true,
                StatDeviation = Settings.StatVariancePercent,
                TypeCount = Math.Max(1, Config.GetText(pk3DS.Core.TextName.Types).Length),
                ModifyStatsFollowEvolutions = Settings.BaseStatsFollowEvolutions,
                ModifyStatsFollowMegas = Settings.BaseStatsFollowMegas,

                EnforceMinimumBST = Settings.EnforceMinimumBST,
                MinBST3Stage1 = Settings.MinBST3Stage1,
                MinBST3Stage2 = Settings.MinBST3Stage2,
                MinBST3Stage3 = Settings.MinBST3Stage3,
                MinBST2Stage1 = Settings.MinBST2Stage1,
                MinBST2Stage2 = Settings.MinBST2Stage2,
                MinBST1Stage = Settings.MinBST1Stage,
                MinBSTLegendary = Settings.MinBSTLegendary,

                EnforceMaximumBST = Settings.EnforceMaximumBST,
                MaxBST3Stage1 = Settings.MaxBST3Stage1,
                MaxBST3Stage2 = Settings.MaxBST3Stage2,
                MaxBST3Stage3 = Settings.MaxBST3Stage3,
                MaxBST2Stage1 = Settings.MaxBST2Stage1,
                MaxBST2Stage2 = Settings.MaxBST2Stage2,
                MaxBST1Stage = Settings.MaxBST1Stage,
                MaxBSTLegendary = Settings.MaxBSTLegendary,

                NoEgregiousStats = Settings.NoEgregiousStats,
                NoEgregiousStatsSingleCap = Settings.NoEgregiousStatsSingleCap,
                NoEgregiousStatsBSTCapRegular = Settings.NoEgregiousStatsBSTCapRegular,
                NoEgregiousStatsBSTCapLegendary = Settings.NoEgregiousStatsBSTCapLegendary,
                AvoidMinmaxing = Settings.AvoidMinmaxing,

                ModifyAbilities = Settings.AbilitiesMode > 0,
                AllowWonderGuard = Settings.AllowWonderGuard,
                BanList = AbilityBans,
                ModifyAbilitiesFollowEvolutions = Settings.AbilitiesFollowEvolutions,
                ModifyAbilitiesFollowMegas = Settings.AbilitiesFollowMegas,

                ModifyTypes = Settings.TypesMode > 0,
                SameTypeChance = Settings.TypesMode == 1 ? 75 : 50,
                ModifyTypesFollowEvolutions = Settings.TypesMode == 1,
                ModifyTypesFollowMegas = Settings.TypesFollowMegas,

                ModifyCatchRate = Settings.WildSetMinCatchRate,
                ModifyHeldItems = Settings.WildRandomizeHeldItems,
                ModifyEggGroup = false,

                TMInheritance = Settings.TMFollowEvolutions,

                ModifyLearnsetTM = Settings.TMHMCompatibilityMode is 1 or 2 || Settings.FullHMCompatibility,
                ModifyLearnsetHM = Settings.TMHMCompatibilityMode is 1 or 2 || Settings.FullHMCompatibility,
                ModifyLearnsetTypeTutors = Settings.MoveTutorCompatibilityMode is 1 or 2,
                ModifyLearnsetMoveTutors = Settings.MoveTutorCompatibilityMode is 1 or 2,

                MegaBSTSync = Settings.CompetitiveRandomizer,
                UseCompetitiveAbilities = Settings.CompetitiveRandomizer
            };

            pRandomizer.Execute();
            pRandomizer.ApplyTypeInheritance();
            pRandomizer.ApplyStatInheritance();
            pRandomizer.ApplyAbilityInheritance();

            if (pRandomizer.MegaBSTSync)
                pRandomizer.ApplyMegaBSTSync();

            // Runs absolute last so it smooths the final shape of every stat spread, Mega forms
            // included, without touching any Pokemon's total BST.
            pRandomizer.ApplyAvoidMinmaxing();

            if (Settings.TMHMCompatibilityMode == 3)
                ApplyFullTMCompatibility(Config.Personal.Table);

            if (Settings.FullHMCompatibility)
                ApplyFullHMCompatibility(Config.Personal.Table);

            if (Settings.MoveTutorCompatibilityMode == 3)
                ApplyFullTutorCompatibility(Config.Personal.Table);

            if (Settings.WildSetMinCatchRate)
            {
                byte minCatch = (byte)(Settings.WildMinCatchRate * 50);
                for (int i = 1; i < Config.Personal.Table.Length; i++)
                    if (Config.Personal.Table[i].CatchRate < minCatch)
                        Config.Personal.Table[i].CatchRate = minCatch;
            }

            if (Settings.FastEggHatching)
            {
                // 1 is the floor the games treat as "hatches almost immediately"; 0 is not used by
                // any vanilla species and is not worth being the first to try.
                for (int i = 1; i < Config.Personal.Table.Length; i++)
                    Config.Personal.Table[i].HatchCycles = 1;
            }

            if (Settings.NoEVsFromPokemon)
            {
                for (int i = 1; i < Config.Personal.Table.Length; i++)
                {
                    var p = Config.Personal.Table[i];
                    p.EV_HP = p.EV_ATK = p.EV_DEF = p.EV_SPE = p.EV_SPA = p.EV_SPD = 0;
                }
            }

            // Write back to GARC (including the reconstructed Master Table for Gen 7 / Gen 6)
            var table = Config.Personal;
            int count = table.Table.Length;
            byte[][] allFiles = new byte[count + 1][];
            for (int i = 0; i < count; i++)
            {
                allFiles[i] = table.Table[i].Write();
            }

            int entryLen = allFiles[0].Length;
            byte[] masterTable = new byte[count * entryLen];
            for (int i = 0; i < count; i++)
            {
                allFiles[i].CopyTo(masterTable, i * entryLen);
            }
            allFiles[count] = masterTable;

            Config.GARCPersonal.Files = allFiles;
            Config.GARCPersonal.Save();
            Config.InitializePersonal();
        }

        if (Settings.EvolutionsMode > 0 || Settings.ChangeImpossibleEvos || Settings.MakeEvosEasier || Settings.RemoveTimeBasedEvos || applyExpansionEvos)
        {
            progressCallback?.Invoke("Randomizing Evolutions & Trade Fixes...", 40);

            var eRandomizer = new EvolutionRandomizer(Config, Config.Evolutions);

            if (Settings.ChangeImpossibleEvos)
                eRandomizer.ExecuteTrade();

            if (Settings.EvolutionsMode == 2)
                eRandomizer.ExecuteEvolveEveryLevel();

            if (Settings.EvolutionsMode == 1)
                eRandomizer.Execute();

            var g = Config.GetGARCData("evolution");
            if (g != null)
            {
                for (int i = 0; i < Config.Evolutions.Length; i++)
                {
                    g.Files[i] = Config.Evolutions[i].Write();
                }
                g.Save();
            }
        }

        // 5. Movesets / Learnsets
        if (Settings.MovesetsMode > 0 || Settings.GuaranteedLevel1Moves)
        {
            progressCallback?.Invoke("Randomizing Pokémon Movesets...", 55);

            var lRandomizer = new LearnsetRandomizer(Config, Config.Learnsets)
            {
                STABPercent = Settings.MovesetsMode == 1 ? 70 : 40,
                STABFirst = true,
                Learn4Level1 = Settings.GuaranteedLevel1Moves,
                OrderByPower = Settings.ReorderDamagingMoves,
                Expand = true,
                ExpandTo = 25
            };

            lRandomizer.Execute();

            var g = Config.GARCLearnsets;
            if (g != null)
            {
                for (int i = 0; i < Config.Learnsets.Length; i++)
                {
                    g.Files[i] = Config.Learnsets[i].Write();
                }
                g.Save();
            }
        }

        ushort[] tmMoveListForInjection = null;
        ushort[][] tutorMoveListsForInjection = null;
        if (Settings.TMHMMovesMode > 0 || Config.MaxSpeciesID >= 1025)
        {
            progressCallback?.Invoke("Randomizing TM move table...", 65);
            tmMoveListForInjection = RandomizeTMMoveTable();
        }
        if (Settings.MoveTutorMovesMode > 0)
        {
            progressCallback?.Invoke("Randomizing Move Tutor Moves...", 67);
            var tutorRand = new TutorRandomizer(RomFSPath, Settings.MoveTutorMovesMode);
            var tutorMoveRand = new MoveRandomizer(Config);
            tutorMoveListsForInjection = tutorRand.Execute(Config.Info.MaxMoveID, tutorMoveRand, Config, Settings.CompetitiveRandomizer);
        }

        // 6. Trainers
        if (Settings.TrainerPokemonMode > 0 || Settings.TrainerLevelModifierPercent != 0)
        {
            progressCallback?.Invoke("Randomizing Trainer Teams & Difficulty...", 70);

            var trdata = Config.GetGARCData("trdata");
            var trpoke = Config.GetGARCData("trpoke");
            var moveRand = new MoveRandomizer(Config);
            var teamGen = new Competitive.TeamGenerator();
            var buildEngine = new Competitive.CompetitiveBuildEngine(Config) { BanList = AbilityBans };
            if (tmMoveListForInjection != null)
                buildEngine.Learnsets.InjectTMMoves(tmMoveListForInjection);
            if (tutorMoveListsForInjection != null)
                buildEngine.Learnsets.InjectTutorMoves(tutorMoveListsForInjection);
            string[] itemNames = Config.GetText(TextName.ItemNames);

            Dictionary<string, List<int>> abilityToFinalSpecies = null;
            if (Settings.CompetitiveRandomizer)
            {
                abilityToFinalSpecies = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                string[] abilityNamesForIndex = Config.GetText(TextName.AbilityNames);
                int maxSpecForIndex = Config.MaxSpeciesID;
                for (int s = 1; s <= maxSpecForIndex; s++)
                {
                    int finalSpec = GetFinalEvolution(s);
                    var fpi = Config.Personal[finalSpec];
                    if (fpi?.Abilities == null) continue;
                    foreach (int abilId in fpi.Abilities)
                    {
                        if (abilId <= 0 || abilId >= abilityNamesForIndex.Length) continue;
                        string abilNameForIndex = abilityNamesForIndex[abilId];
                        if (string.IsNullOrEmpty(abilNameForIndex)) continue;
                        if (!abilityToFinalSpecies.TryGetValue(abilNameForIndex, out var list))
                            abilityToFinalSpecies[abilNameForIndex] = list = new List<int>();
                        if (!list.Contains(finalSpec)) list.Add(finalSpec);
                    }
                }
            }

            if (trdata != null && trpoke != null)
            {
                byte[][] trd = trdata.Files;
                byte[][] trp = trpoke.Files;

                if (Config.Generation == 7)
                {
                    for (int i = 1; i < trd.Length && i < trp.Length; i++)
                    {
                        if (trp[i].Length < 4) continue;
                        var trainer = new TrainerData7(trd[i], trp[i]);
                        if (trainer.Pokemon == null || trainer.Pokemon.Count == 0) continue;

                        if (Settings.BetterTrainerMovesets || Settings.CompetitiveRandomizer || Settings.MasterAIMode > 0)
                            trainer.Flag = true;

                        bool fullAI = Settings.CompetitiveRandomizer
                                   || Settings.MasterAIMode == 2
                                   || (Settings.MasterAIMode == 1 && TrainerTiers.Of(trainer, i) != TrainerTiers.Tier.Regular);

                        if (fullAI)
                        {
                            trainer.AI |= (int)(TrainerAI.Basic | TrainerAI.Strong | TrainerAI.Expert
                                              | TrainerAI.PokeChange | TrainerAI.UseItem);
                        }

                        bool shouldAddHeldItems = Settings.AddHeldItemsBossTrainers || Settings.AddHeldItemsImportantTrainers || Settings.AddHeldItemsRegularTrainers || Settings.CompetitiveRandomizer;
                        int[] sensibleItems = [217, 229, 158, 270, 268, 297, 287, 275, 538, 540, 640, 269, 271, 272, 231, 232, 233, 234, 235];

                        bool isTutorialBattle = FirstRivalBattle.Matches(i, trainer);

                        // 1. Team Expansion FIRST so full team size is known
                        int addCount = 0;
                        if (Settings.AddPokemonBossTrainers) addCount = Math.Max(addCount, Settings.AddPokemonBossCount);
                        if (Settings.AddPokemonImportantTrainers) addCount = Math.Max(addCount, Settings.AddPokemonImportantCount);
                        if (Settings.AddPokemonRegularTrainers) addCount = Math.Max(addCount, Settings.AddPokemonRegularCount);
                        if (isTutorialBattle) addCount = 0;

                        if (addCount > 0 && trainer.Pokemon.Count > 0 && trainer.Pokemon.Count < 6)
                        {
                            byte avgLevel = (byte)trainer.Pokemon.Average(p => p.Level);
                            int targetCount = Math.Min(6, trainer.Pokemon.Count + addCount);
                            while (trainer.Pokemon.Count < targetCount)
                            {
                                ushort extraSpecies = (ushort)speciesRand.GetRandomSpecies(trainer.Pokemon[0].Species);
                                if ((Settings.ForceFullyEvolvedAtLevel && avgLevel >= Settings.ForceFullyEvolvedLevel) || Settings.CompetitiveRandomizer)
                                    extraSpecies = GetFinalEvolution(extraSpecies);
                                var newPk = new TrainerPoke7
                                {
                                    Species = extraSpecies,
                                    Level = avgLevel,
                                };
                                trainer.Pokemon.Add(newPk);
                            }
                            trainer.NumPokemon = trainer.Pokemon.Count;
                        }

                        // 2. Generate Archetype & Roles for full team
                        var archetype = teamGen.GetRandomArchetype();
                        var roles = teamGen.GetRolesForArchetype(archetype, trainer.Pokemon.Count);
                        var archetypeSpec = teamGen.GetSpecification(archetype);
                        bool assignedMega = Settings.NoMegaEvolution;
                        bool assignedZCrystal = Settings.NoZMoves;
                        var teamAbilitiesCovered = new List<string>();

                        var teamTypesCovered = new HashSet<int>();

                        // Per-trainer bookkeeping for "avoid duplicates" and "force diverse types".
                        // Both reset for every trainer: they are about one team, not the whole game.
                        var placedSpecies = new HashSet<int>();
                        var teamTypes = new HashSet<int>();
                        bool forceDiverse = TrainerTiers.Pick(trainer, i,
                            Settings.TrainerDiverseTypesBoss,
                            Settings.TrainerDiverseTypesImportant,
                            Settings.TrainerDiverseTypesRegular);

                        int trainerTheme = -1;
                        if (TrainerThemes.UsesThemes(Settings.TrainerPokemonMode))
                        {
                            var speciesBefore = trainer.Pokemon.Select(p => p.Species).ToList();
                            trainerTheme = TrainerThemes.ThemeFor(
                                Config, Settings.TrainerPokemonMode, trainer, i, speciesBefore);
                        }

                        // 3. Process every Pokemon in the team
                        for (int pkIdx = 0; pkIdx < trainer.Pokemon.Count; pkIdx++)
                        {
                            var pk = trainer.Pokemon[pkIdx];
                            var role = (pkIdx < roles.Length) ? roles[pkIdx] : Competitive.CompetitiveRole.OffensiveSweeper;
                            int preMapSpecies = pk.Species;

                            if (Settings.TrainerPokemonMode > 0 || Settings.CompetitiveRandomizer)
                            {
                                pk.Species = (ushort)PickTrainerSpecies(speciesRand, pk.Species, trainerTheme);
                                pk.Species = (ushort)RefineTrainerPick(
                                    speciesRand, preMapSpecies, pk.Species, placedSpecies, teamTypes, forceDiverse, trainerTheme);
                            }

                            int requiredAbilityId = -1;
                            if (Settings.CompetitiveRandomizer && archetypeSpec != null)
                            {
                                string[] abilityNamesForBias = Config.GetText(TextName.AbilityNames);
                                var wantedAbilityIds = new List<int>();
                                var wantedNames = new List<string>();
                                if (pkIdx == 0)
                                {
                                    if (!string.IsNullOrEmpty(archetypeSpec.RequiredLeadAbility))
                                        wantedNames.Add(archetypeSpec.RequiredLeadAbility);
                                    if (archetypeSpec.AlternateLeadAbilities?.Length > 0)
                                        wantedNames.AddRange(archetypeSpec.AlternateLeadAbilities);
                                }
                                else if (archetypeSpec.RequiredTeammateAbilities.Count > 0)
                                {
                                    string stillNeeded = archetypeSpec.RequiredTeammateAbilities.FirstOrDefault(
                                        a => !teamAbilitiesCovered.Any(c => c.Equals(a, StringComparison.OrdinalIgnoreCase)));
                                    if (stillNeeded != null) wantedNames.Add(stillNeeded);
                                }

                                foreach (string n in wantedNames)
                                {
                                    int id = Array.FindIndex(abilityNamesForBias, a => a.Equals(n, StringComparison.OrdinalIgnoreCase));
                                    if (id > 0) wantedAbilityIds.Add(id);
                                }

                                var typesStillNeeded = new HashSet<int>(archetypeSpec.RequiredTypes);
                                typesStillNeeded.ExceptWith(teamTypesCovered);

                                int winningSpecies = GetFinalEvolution(pk.Species);
                                bool foundWantedAbility = false;

                                if (wantedAbilityIds.Count > 0 && abilityToFinalSpecies != null)
                                {
                                    var shuffledWanted = wantedNames.OrderBy(_ => Util.Rand.Next()).ToList();
                                    foreach (string wanted in shuffledWanted)
                                    {
                                        if (!abilityToFinalSpecies.TryGetValue(wanted, out var matches) || matches.Count == 0)
                                            continue;

                                        var pick = matches
                                            .Where(s => Competitive.TeamGenerator.IsStatSuitableForRole(Config.Personal[s], role, archetypeSpec))
                                            .ToList();

                                        if (archetypeSpec.RequiredTypes.Count > 0 && typesStillNeeded.Count > 0)
                                        {
                                            var typed = pick
                                                .Where(s => Config.Personal[s]?.Types?.Any(typesStillNeeded.Contains) == true)
                                                .ToList();
                                            if (typed.Count > 0) pick = typed;
                                        }

                                        if (pick.Count == 0) pick = matches;

                                        winningSpecies = pick[Util.Rand.Next(pick.Count)];
                                        requiredAbilityId = Array.FindIndex(abilityNamesForBias, a => a.Equals(wanted, StringComparison.OrdinalIgnoreCase));
                                        foundWantedAbility = true;
                                        break;
                                    }
                                }

                                if (!foundWantedAbility)
                                {
                                    // Manifesto 0.6 fixes this budget; it lives in CompetitiveRules
                                    // so the document and the generator cannot drift apart again.
                                    const int maxArchetypeFitAttempts = Competitive.CompetitiveRules.ConstrainedPickAttempts;
                                    int candidateSpecies = pk.Species;
                                    for (int attempt = 0; attempt < maxArchetypeFitAttempts; attempt++)
                                    {
                                        int evaluatedSpecies = GetFinalEvolution(candidateSpecies);
                                        var candidatePI = Config.Personal[evaluatedSpecies];
                                        winningSpecies = evaluatedSpecies;
                                        if (wantedAbilityIds.Count > 0)
                                        {
                                            int matchAbilId = candidatePI?.Abilities?.FirstOrDefault(a => wantedAbilityIds.Contains(a)) ?? 0;
                                            if (matchAbilId > 0)
                                            {
                                                requiredAbilityId = matchAbilId;
                                                break;
                                            }
                                        }
                                        else if (Competitive.TeamGenerator.IsStatSuitableForRole(candidatePI, role, archetypeSpec) &&
                                                 (typesStillNeeded.Count == 0 ||
                                                  candidatePI?.Types?.Any(typesStillNeeded.Contains) == true ||
                                                  attempt >= maxArchetypeFitAttempts / 2))
                                        {
                                            break;
                                        }

                                        candidateSpecies = speciesRand.GetRandomSpecies(preMapSpecies);
                                    }
                                }
                                pk.Species = (ushort)winningSpecies;
                            }

                            if ((Settings.ForceFullyEvolvedAtLevel && pk.Level >= Settings.ForceFullyEvolvedLevel) || Settings.CompetitiveRandomizer)
                                pk.Species = GetFinalEvolution(pk.Species);

                            if (Settings.TrainerLevelModifierPercent != 0)
                                pk.Level = (byte)Math.Min(100, (int)Math.Max(1, pk.Level * (1 + (Settings.TrainerLevelModifierPercent / 100m))));

                            if (Settings.TrainerRandomShiny)
                                pk.Shiny = (int)(Util.Random32() % 100) < 5;

                            var pi = Config.Personal[pk.Species];
                            string[] abilityNames = Config.GetText(TextName.AbilityNames);

                            if (Settings.CompetitiveRandomizer)
                            {
                                // If the archetype-fit search above locked in a required lead
                                // ability, force that exact slot; otherwise pick the best tier.
                                int forcedSlot = requiredAbilityId > 0 ? FindAbilitySlot(pi, requiredAbilityId) : 0;
                                pk.Ability = forcedSlot > 0 ? forcedSlot : buildEngine.ChooseCompetitiveAbilitySlot(pi, abilityNames);
                            }
                            string abilName = Competitive.CompetitiveBuildEngine.ResolveAbilityName(pi, pk.Ability, abilityNames);
                            if (Settings.CompetitiveRandomizer && !string.IsNullOrEmpty(abilName))
                                teamAbilitiesCovered.Add(abilName);
                            if (Settings.CompetitiveRandomizer && pi?.Types != null)
                                foreach (int t in pi.Types) teamTypesCovered.Add(t);

                            if (Settings.CompetitiveRandomizer)
                            {
                                string[] moveNames = Config.GetText(TextName.MoveNames);
                                pk.Moves = buildEngine.BuildCompetitiveMoveset(pk.Species, role, pi, moveRand, abilName, archetype, Config.Moves);
                                var (evs, nature) = buildEngine.BuildEVsAndNature(pi, role, pk.Moves, archetype == Competitive.TeamArchetype.TrickRoom);
                                pk.EVs = evs.Select(b => (int)b).ToArray();
                                pk.Nature = nature;

                                int[] ivs = Settings.TrainerFullIVs
                                    ? [31, 31, 31, 31, 31, 31]
                                    : [Util.Rand.Next(32), Util.Rand.Next(32), Util.Rand.Next(32), Util.Rand.Next(32), Util.Rand.Next(32), Util.Rand.Next(32)];
                                // Trick Room teams need 0 Speed IV to be as slow as possible,
                                // regardless of the Full IVs toggle (TrainerPoke7.IVs = [HP, Atk, Def, SpA, SpD, Spe]).
                                if (archetype == Competitive.TeamArchetype.TrickRoom)
                                    ivs[5] = 0;
                                pk.IVs = ivs;

                                pk.Item = (int)buildEngine.AssignItem(pk.Species, pi, abilName, pk.Moves, role, itemNames, moveNames, Config.Moves, ref assignedMega, ref assignedZCrystal, archetype, teamAbilitiesCovered);

                                string heldItemName = pk.Item > 0 && pk.Item < itemNames.Length ? itemNames[pk.Item] : "";
                                if (Competitive.CompetitiveValidator.IsMegaStone(heldItemName) &&
                                    Competitive.CompetitiveDatabase.MegaStoneMap.TryGetValue(pk.Species, out var heldStones))
                                {
                                    int stoneIdx = Array.FindIndex(heldStones, s => s.Equals(heldItemName, StringComparison.OrdinalIgnoreCase));
                                    var megaFormIdxs = Competitive.CompetitiveDatabase.GetMegaFormIndices(pk.Species, Config.Personal.Table);
                                    if (stoneIdx >= 0 && stoneIdx < megaFormIdxs.Length)
                                    {
                                        var megaPi = Config.Personal[megaFormIdxs[stoneIdx]];

                                        string megaAbilName = Competitive.CompetitiveBuildEngine.ResolveAbilityName(megaPi, 1, abilityNames);
                                        if (!string.IsNullOrEmpty(megaAbilName))
                                        {
                                            teamAbilitiesCovered.Remove(abilName);
                                            teamAbilitiesCovered.Add(megaAbilName);
                                            abilName = megaAbilName;
                                        }

                                        pk.Moves = buildEngine.BuildCompetitiveMoveset(pk.Species, role, megaPi, moveRand, abilName, archetype, Config.Moves);
                                        var (megaEvs, megaNature) = buildEngine.BuildEVsAndNature(megaPi, role, pk.Moves, archetype == Competitive.TeamArchetype.TrickRoom);
                                        pk.EVs = megaEvs.Select(b => (int)b).ToArray();
                                        pk.Nature = megaNature;
                                        pi = megaPi;
                                    }
                                }

                                Competitive.CompetitiveValidator.ValidateAndSanitize(
                                    pk, pi, abilName, role, itemNames,
                                    moveNames, Config.Moves, buildEngine.Learnsets);
                            }
                            else
                            {
                                if (Settings.BetterTrainerMovesets)
                                    pk.Moves = moveRand.GetRandomMoveset(pk.Species);
                                if (shouldAddHeldItems && (pk.Item == 0 || Settings.WildRandomizeHeldItems))
                                    pk.Item = sensibleItems[Util.Random32() % sensibleItems.Length];

                                Competitive.CompetitiveValidator.ValidateAndSanitize(
                                    pk, pi, abilName, role, itemNames,
                                    Config.GetText(TextName.MoveNames), Config.Moves, buildEngine.Learnsets);
                            }

                            // Recorded at the end, using whatever the slot finally settled on - the
                            // competitive path above can still change it after the initial pick.
                            placedSpecies.Add(pk.Species);
                            foreach (int t in TypeRestrictions.TypesOf(Config, pk.Species))
                                teamTypes.Add(t);
                        }

                        // Ensure Mega Stone and/or Z-Crystal on competitive / boss teams if not assigned yet
                        if (Settings.CompetitiveRandomizer && trainer.Pokemon.Count >= 2)
                        {
                            if (!assignedMega)
                            {
                                int megaIdx = trainer.Pokemon.FindIndex(p => Competitive.CompetitiveDatabase.MegaStoneMap.ContainsKey(p.Species));
                                if (megaIdx < 0)
                                {
                                    // Transform slot 0 into a Mega-capable species
                                    var megaKeys = Competitive.CompetitiveDatabase.MegaStoneMap.Keys.ToArray();
                                    int chosenMegaSpecies = megaKeys[Util.Rand.Next(megaKeys.Length)];
                                    trainer.Pokemon[0].Species = (ushort)chosenMegaSpecies;
                                    megaIdx = 0;
                                }
                                var megaPk = trainer.Pokemon[megaIdx];
                                ushort megaItem = buildEngine.TryAssignMegaStone(megaPk.Species, itemNames, ref assignedMega);
                                if (megaItem > 0) megaPk.Item = megaItem;
                            }

                            if (!assignedZCrystal && Config.Generation == 7)
                            {
                                var zPk = trainer.Pokemon.FirstOrDefault(p => p.Item == 0 || !Competitive.CompetitiveValidator.IsMegaStone(itemNames.ElementAtOrDefault(p.Item)));
                                if (zPk != null)
                                {
                                    var zpi = Config.Personal[zPk.Species];
                                    ushort zItem = buildEngine.TryAssignZCrystal(zPk.Species, zpi, zPk.Moves, itemNames, Config.GetText(TextName.MoveNames), Config.Moves, ref assignedZCrystal);
                                    if (zItem > 0) zPk.Item = zItem;
                                }
                            }
                        }

                        ApplyBattleStyle(trainer);

                        if (isTutorialBattle) FirstRivalBattle.Enforce(trainer);

                        trainer.Write(out byte[] outTrData, out byte[] outTrPoke);
                        trd[i] = outTrData;
                        trp[i] = outTrPoke;
                    }
                }
                else if (Config.Generation == 6)
                {
                    for (int i = 1; i < trd.Length && i < trp.Length; i++)
                    {
                        if (trp[i].Length < 4) continue;
                        var trainer = new TrainerData6(trd[i], trp[i], Config.ORAS);
                        if (trainer.Team == null || trainer.Team.Length == 0) continue;

                        foreach (var pk in trainer.Team)
                        {
                            if (pk == null) continue;
                            if (Settings.TrainerPokemonMode > 0 || Settings.CompetitiveRandomizer)
                                pk.Species = (ushort)speciesRand.GetMappedSpecies(pk.Species);

                            if ((Settings.ForceFullyEvolvedAtLevel && pk.Level >= Settings.ForceFullyEvolvedLevel) || Settings.CompetitiveRandomizer)
                                pk.Species = GetFinalEvolution(pk.Species);

                            if (Settings.TrainerLevelModifierPercent != 0)
                                pk.Level = (ushort)Math.Min(100, (int)Math.Max(1, pk.Level * (1 + (Settings.TrainerLevelModifierPercent / 100m))));

                            if (Settings.BetterTrainerMovesets)
                            {
                                int[] moves = moveRand.GetRandomMoveset(pk.Species);
                                pk.Moves[0] = (ushort)moves[0];
                                pk.Moves[1] = (ushort)moves[1];
                                pk.Moves[2] = (ushort)moves[2];
                                pk.Moves[3] = (ushort)moves[3];
                            }
                        }

                        // Team Expansion
                        int addCount = 0;
                        if (Settings.AddPokemonBossTrainers) addCount = Math.Max(addCount, Settings.AddPokemonBossCount);
                        if (Settings.AddPokemonImportantTrainers) addCount = Math.Max(addCount, Settings.AddPokemonImportantCount);
                        if (Settings.AddPokemonRegularTrainers) addCount = Math.Max(addCount, Settings.AddPokemonRegularCount);

                        if (addCount > 0 && trainer.Team.Length > 0 && trainer.Team.Length < 6)
                        {
                            ushort avgLevel = (ushort)trainer.Team.Where(p => p != null).Average(p => p.Level);
                            var teamList = trainer.Team.Where(p => p != null).ToList();
                            int targetCount = Math.Min(6, teamList.Count + addCount);
                            while (teamList.Count < targetCount)
                            {
                                ushort extraSpecies = (ushort)speciesRand.GetRandomSpecies(teamList[0].Species);
                                if ((Settings.ForceFullyEvolvedAtLevel && avgLevel >= Settings.ForceFullyEvolvedLevel) || Settings.CompetitiveRandomizer)
                                    extraSpecies = GetFinalEvolution(extraSpecies);
                                var newPk = new TrainerData6.Pokemon(new byte[trainer.Team[0].Write(trainer.Item, trainer.Moves).Length], trainer.Item, trainer.Moves)
                                {
                                    Species = extraSpecies,
                                    Level = avgLevel
                                };
                                if (Settings.BetterTrainerMovesets)
                                {
                                    int[] moves = moveRand.GetRandomMoveset(extraSpecies);
                                    newPk.Moves[0] = (ushort)moves[0];
                                    newPk.Moves[1] = (ushort)moves[1];
                                    newPk.Moves[2] = (ushort)moves[2];
                                    newPk.Moves[3] = (ushort)moves[3];
                                }
                                teamList.Add(newPk);
                            }
                            trainer.Team = teamList.ToArray();
                            trainer.NumPokemon = (byte)trainer.Team.Length;
                        }

                        trd[i] = trainer.Write();
                        trp[i] = trainer.WriteTeam();
                    }
                }

                trdata.Save();
                trpoke.Save();
            }
        }

        // 7. Wild Encounters
        if (Settings.WildPokemonMode > 0 && wildRandomizerAction != null)
        {
            progressCallback?.Invoke("Randomizing Wild Encounters...", 85);
            var formRand = new FormRandomizer(Config)
            {
                AllowMega = Settings.WildAllowAltFormes,
                AllowAlolanForm = Settings.WildAllowAltFormes
            };
            wildRandomizerAction.Invoke(speciesRand, formRand);
        }

        // 8. Static Encounters
        if (Settings.StaticsMode > 0 && staticsRandomizerAction != null)
        {
            progressCallback?.Invoke("Randomizing Static Encounters...", 88);
            var formRand = new FormRandomizer(Config)
            {
                AllowMega = Settings.StaticsAllowAltFormes,
                AllowAlolanForm = Settings.StaticsAllowAltFormes
            };
            staticsRandomizerAction.Invoke(speciesRand, formRand);
        }

        if (Settings.SpecialShopsMode > 0 || Settings.RandomizeAllShops)
        {
            progressCallback?.Invoke("Randomizing Special Shops & Poké Marts...", 95);

            var martRand = new MartRandomizer(RomFSPath, Settings.SpecialShopsMode, Settings.ShopBanBadItems, Settings.RandomizeAllShops);
            if (Settings.CompetitiveRandomizer || Settings.RandomizeAllShops)
            {
                martRand.ExecuteCompetitive(Config.Info.MaxItemID, Config);

                // Expansion failing is not fatal - the shops are still filled - but it changes what
                // was asked for, so it is said out loud rather than left to be noticed in game.
                if (!string.IsNullOrEmpty(MartRandomizer.ExpansionSkipped))
                    progressCallback?.Invoke("WARNING: " + MartRandomizer.ExpansionSkipped, 95);
            }
            else
                martRand.Execute(Config.Info.MaxItemID);
        }

        // 9a1. Cheap Rare Candies
        if (Settings.CheapRareCandies)
        {
            progressCallback?.Invoke("Making Rare Candies cheap and stocking them...", 93);
            string candyResult = ApplyCheapRareCandies();
            if (!string.IsNullOrEmpty(candyResult))
                progressCallback?.Invoke(candyResult, 93);
        }

        // 9a2. Level caps
        if (Settings.EnableLevelCaps)
        {
            progressCallback?.Invoke("Installing Level Caps...", 94);
            string capResult = InstallLevelCaps();
            if (!string.IsNullOrEmpty(capResult))
                progressCallback?.Invoke(capResult, 94);
        }

        // 9a2b. Research Center recipes the user asked for
        if (Settings.InstallPatches is { Count: > 0 })
        {
            progressCallback?.Invoke("Installing selected patches...", 94);
            foreach (string line in InstallSelectedRecipes())
                progressCallback?.Invoke(line, 94);
        }

        // 9a2c. Custom EXP Multiplier
        if (Settings.CustomExpMultiplier && Settings.ExpMultiplier >= 1)
        {
            progressCallback?.Invoke($"Applying {Settings.ExpMultiplier}x EXP Multiplier patch...", 94);
            var expLog = new List<string>();
            ApplyExpMultiplier(Config, Settings.ExpMultiplier, expLog);
            foreach (string line in expLog)
                progressCallback?.Invoke(line, 94);
        }

        // 9a3. Type effectiveness chart
        if (Settings.TypeEffectivenessMode > 0)
        {
            progressCallback?.Invoke("Rewriting the type effectiveness chart...", 94);
            string typeResult = InstallTypeEffectiveness();
            if (!string.IsNullOrEmpty(typeResult))
                progressCallback?.Invoke(typeResult, 94);
        }

        // 9b. Starters
        if (Settings.StartersMode > 0)
        {
            progressCallback?.Invoke("Randomizing Starters...", 92);
            RandomizeStarters(speciesRand);
        }

        // 9c. In-game trades
        if (Settings.TradesMode > 0)
        {
            progressCallback?.Invoke("Randomizing In-Game Trades...", 93);
            RandomizeTrades(speciesRand);
        }

        // 10. Pickup items
        if (Settings.PickupItemsMode > 0)
        {
            progressCallback?.Invoke("Randomizing Pickup Items...", 97);
            RandomizePickup();
        }

        progressCallback?.Invoke("Universal Randomization finished successfully!", 100);
    }

    /// <summary>
    /// The three starters the professor offers.
    /// </summary>
    private void RandomizeStarters(SpeciesRandomizer speciesRand)
    {
        const int stride = 0x14;
        try
        {
            var g = Config.GetGARCData("encounterstatic");
            if (g?.Files == null || g.Files.Length == 0) return;

            byte[] data = g.Files[0];
            if (data == null || data.Length < 3 * stride) return;

            int[] custom = [Settings.CustomStarter1, Settings.CustomStarter2, Settings.CustomStarter3];
            int[] currents = [BitConverter.ToUInt16(data, 0), BitConverter.ToUInt16(data, stride), BitConverter.ToUInt16(data, 2 * stride)];

            // Custom starters are exactly what the user typed; a type restriction on top of that
            // would be overriding the one mode that exists to not be random.
            int[] chosen = Settings.StartersMode == 1
                ? custom
                : PickStarterTrio(speciesRand, currents);

            for (int i = 0; i < 3; i++)
            {
                int species = chosen[i];
                if (species <= 0 || species > Config.MaxSpeciesID) continue;

                int offset = i * stride;
                BitConverter.GetBytes((ushort)species).CopyTo(data, offset);
                data[offset + 2] = 0;   // base forme: an alternate forme here can be a species the game cannot show yet
            }

            g.Files[0] = data;
            g.Save();

            UpdateStarterStoryText(chosen);
        }
        catch
        {
            // Never abort the run for this - everything before it has already been written.
        }
    }

    private void UpdateStarterStoryText(int[] chosen)
    {
        if (chosen == null || chosen.Length < 3 || Config == null) return;
        try
        {
            string[] speciesNames = Config.GetText(TextName.SpeciesNames);
            string[] typeNames = Config.GetText(TextName.Types);

            if (Config.Generation == 7)
            {
                var storyGarc = Config.GetGARCData("storytext");
                if (storyGarc?.Files == null || storyGarc.Files.Length == 0) return;

                int textFileIndex = Config.USUM ? 41 : 39;
                if (textFileIndex >= storyGarc.Files.Length) return;

                string[] lines = TextFile.GetStrings(Config, storyGarc.Files[textFileIndex]);
                if (lines == null || lines.Length == 0) return;

                if (Config.USUM)
                {
                    // Descriptors (lines 1, 2, 3)
                    if (lines.Length > 3)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            int spec = chosen[i];
                            if (spec <= 0 || spec >= speciesNames.Length) continue;
                            string sName = speciesNames[spec];
                            int lineIdx = i + 1;
                            int sep = lines[lineIdx].IndexOfAny([':', ',']);
                            if (sep >= 0)
                                lines[lineIdx] = sName + lines[lineIdx].Substring(sep);
                            else
                                lines[lineIdx] = sName;
                        }
                    }

                    // Confirmation dialogues (lines 7, 8, 9) and Menu options (lines 14, 15, 16)
                    for (int i = 0; i < 3; i++)
                    {
                        int spec = chosen[i];
                        if (spec <= 0 || spec >= speciesNames.Length) continue;
                        string sName = speciesNames[spec];
                        int typeId = Config.Personal?.Table != null && spec < Config.Personal.Table.Length
                            ? Config.Personal.Table[spec].Types[0]
                            : 0;
                        string tName = typeId < typeNames.Length ? typeNames[typeId] : "Normal";

                        int confirmIdx = i + 7;
                        if (confirmIdx < lines.Length)
                        {
                            string orig = lines[confirmIdx];
                            string varTag = orig.Contains("[VAR") ? orig.Substring(orig.IndexOf("[VAR")) : "";
                            lines[confirmIdx] = $"So, you wanna go with the {tName}-type Pokémon\\n{sName}?{varTag}";
                        }

                        int optionIdx = i + 14;
                        if (optionIdx < lines.Length)
                        {
                            lines[optionIdx] = sName;
                        }
                    }
                }
                else if (Config.SM)
                {
                    // Descriptors (lines 11, 12, 13)
                    if (lines.Length > 13)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            int spec = chosen[i];
                            if (spec <= 0 || spec >= speciesNames.Length) continue;
                            string sName = speciesNames[spec];
                            int lineIdx = i + 11;
                            int sep = lines[lineIdx].IndexOfAny([':', ',']);
                            if (sep >= 0)
                                lines[lineIdx] = sName + lines[lineIdx].Substring(sep);
                            else
                                lines[lineIdx] = sName;
                        }
                    }

                    // Menu options (lines 1, 2, 3), Confirmations (lines 4, 5, 6), Flavor (lines 35, 36, 37)
                    for (int i = 0; i < 3; i++)
                    {
                        int spec = chosen[i];
                        if (spec <= 0 || spec >= speciesNames.Length) continue;
                        string sName = speciesNames[spec];
                        int typeId = Config.Personal?.Table != null && spec < Config.Personal.Table.Length
                            ? Config.Personal.Table[spec].Types[0]
                            : 0;
                        string tName = typeId < typeNames.Length ? typeNames[typeId] : "Normal";

                        int optionIdx = i + 1;
                        if (optionIdx < lines.Length)
                        {
                            lines[optionIdx] = $"The {tName}-type {sName}";
                        }

                        int confirmIdx = i + 4;
                        if (confirmIdx < lines.Length)
                        {
                            string orig = lines[confirmIdx];
                            string varTag = orig.Contains("[VAR") ? orig.Substring(orig.IndexOf("[VAR")) : "";
                            lines[confirmIdx] = $"Will you choose the {tName}-type Pokémon\\n{sName}?{varTag}";
                        }

                        int flavorIdx = i + 35;
                        if (flavorIdx < lines.Length && lines[flavorIdx].Contains("\\n"))
                        {
                            string flavorSub = lines[flavorIdx].Substring(lines[flavorIdx].IndexOf("\\n"));
                            lines[flavorIdx] = $"The {tName}-type {sName}{flavorSub}";
                        }
                    }
                }

                storyGarc.Files[textFileIndex] = TextFile.GetBytes(Config, lines);
                storyGarc.Save();
            }
            else if (Config.Generation == 6)
            {
                var gameTextGarc = Config.GARCGameText ?? Config.GetGARCData("gametext");
                if (gameTextGarc?.Files != null && gameTextGarc.Files.Length > 0)
                {
                    int textFileIndex = Config.XY ? 63 : 77;
                    if (textFileIndex < gameTextGarc.Files.Length)
                    {
                        string[] lines = TextFile.GetStrings(Config, gameTextGarc.Files[textFileIndex]);
                        if (lines != null && lines.Length > 3)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                int spec = chosen[i];
                                if (spec <= 0 || spec >= speciesNames.Length) continue;
                                lines[i + 1] = speciesNames[spec];
                            }
                            gameTextGarc.Files[textFileIndex] = TextFile.GetBytes(Config, lines);
                            gameTextGarc.Save();
                        }
                    }
                }
            }
        }
        catch
        {
            // Non-critical, continue
        }
    }

    /// <summary>Whether a trainer is on the game's important-trainer list.</summary>
    private static bool IsImportantTrainer(int index) => TrainerTiers.IsImportant(index);

    /// <summary>
    /// Sets a trainer's battle format, and gives them enough Pokemon to fight it.
    /// </summary>
    private void ApplyBattleStyle(TrainerData7 trainer)
    {
        if (trainer == null) return;

        BattleMode? want = Settings.BattleStyleMode switch
        {
            1 => Util.Rand.Next(2) == 0 ? BattleMode.Singles : BattleMode.Doubles,
            2 => Settings.BattleStyleChoice == 1 ? BattleMode.Doubles : BattleMode.Singles,
            _ => Settings.DoubleBattleMode ? BattleMode.Doubles : null,
        };
        if (want is not { } mode) return;

        // Leave multi battles alone whatever the setting says: they are scripted pairs, and
        // rewriting one to Singles orphans the partner trainer the script still expects.
        if (trainer.Mode == BattleMode.Multi) return;

        trainer.Mode = mode;

        if (mode == BattleMode.Doubles && trainer.Pokemon.Count == 1)
        {
            trainer.Pokemon.Add(trainer.Pokemon[0].Clone());
            trainer.NumPokemon = trainer.Pokemon.Count;
        }
    }

    /// <summary>
    /// The species this trainer slot starts from, before duplicate and diversity refinement.
    /// </summary>
    private int PickTrainerSpecies(SpeciesRandomizer speciesRand, int original, int theme)
    {
        // A themed trainer's slots must land on the theme, so that wins over how the mode would
        // otherwise pick: an even-distribution draw that ignored the theme would simply undo it.
        if (theme >= 0)
            return speciesRand.GetRandomSpeciesType(original, theme);

        return Settings.TrainerPokemonMode switch
        {
            TrainerThemes.RandomEvenDistribution => speciesRand.GetEvenDistributionSpecies(),
            _ => speciesRand.GetMappedSpecies(original),
        };
    }

    /// <summary>
    /// Re-picks a trainer's slot when it repeats a species, or a type the team already covers.
    /// </summary>
    private int RefineTrainerPick(SpeciesRandomizer speciesRand, int original, int chosen,
                                  HashSet<int> placed, HashSet<int> teamTypes, bool diverse, int theme = -1)
    {
        bool avoid = Settings.TrainerAvoidDuplicates;
        if (theme >= 0) diverse = false;
        if (!avoid && !diverse) return chosen;

        int Reroll() => theme >= 0
            ? speciesRand.GetRandomSpeciesType(original, theme)
            : speciesRand.GetRandomSpecies(original);

        bool InRange(int s) => s > 0 && s <= Config.MaxSpeciesID;
        bool NotDuplicate(int s) => !avoid || !placed.Contains(s);
        bool AddsType(int s) => !diverse || TypeRestrictions.TypesOf(Config, s).Any(t => !teamTypes.Contains(t));

        if (InRange(chosen) && NotDuplicate(chosen) && AddsType(chosen)) return chosen;

        // Both constraints first.
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int c = Reroll();
            if (InRange(c) && NotDuplicate(c) && AddsType(c)) return c;
        }

        // Then just the duplicate rule, which is the one a player actually notices.
        if (avoid)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int c = Reroll();
                if (InRange(c) && !placed.Contains(c)) return c;
            }
        }

        return chosen;
    }

    /// <summary>
    /// The three starters, honouring both the pick mode and the type restriction.
    /// </summary>
    private int[] PickStarterTrio(SpeciesRandomizer speciesRand, int[] currents)
    {
        int Pick(int current) => Settings.StartersMode == 3
            ? PickStarterWithEvolutions(speciesRand, current)
            : speciesRand.GetRandomSpecies(current);

        bool Acceptable(int species) =>
            species > 0 && species <= Config.MaxSpeciesID &&
            (!Settings.StarterNoDualTypes || TypeRestrictions.IsMonoType(Config, species));

        // A species of a required type, or 0 when the pool has none after a bounded search.
        int PickOfType(int current, int type)
        {
            for (int attempt = 0; attempt < 300; attempt++)
            {
                int c = Pick(current);
                if (Acceptable(c) && TypeRestrictions.HasType(Config, c, type)) return c;
            }
            return 0;
        }

        int PickAny(int current)
        {
            for (int attempt = 0; attempt < 300; attempt++)
            {
                int c = Pick(current);
                if (Acceptable(c)) return c;
            }
            return speciesRand.GetRandomSpecies(current);
        }

        switch (Settings.StarterTypeRestriction)
        {
            case 1: // Fire, Water, Grass
            case 2: // any type triangle
            {
                var triangles = Settings.StarterTypeRestriction == 1
                    ? [[Competitive.PokemonTypes.Fire, Competitive.PokemonTypes.Water, Competitive.PokemonTypes.Grass]]
                    : TypeRestrictions.FindTypeTriangles();

                // Shuffled so a failed triangle is not retried in the same order every run.
                var order = triangles.OrderBy(_ => Util.Rand.Next()).ToList();
                foreach (var triangle in order)
                {
                    int[] picks = new int[3];
                    bool ok = true;
                    for (int i = 0; i < 3 && ok; i++)
                    {
                        picks[i] = PickOfType(currents[i], triangle[i]);
                        if (picks[i] == 0) ok = false;
                    }
                    if (ok) return picks;
                }
                break;
            }

            case 3: // unique - no two starters share a type
            {
                for (int attempt = 0; attempt < 400; attempt++)
                {
                    int[] picks = [PickAny(currents[0]), PickAny(currents[1]), PickAny(currents[2])];
                    if (!TypeRestrictions.SharesType(Config, picks[0], picks[1]) &&
                        !TypeRestrictions.SharesType(Config, picks[0], picks[2]) &&
                        !TypeRestrictions.SharesType(Config, picks[1], picks[2]))
                        return picks;
                }
                break;
            }

            case 4: // all three of one chosen type
            {
                int type = Settings.StarterSingleType;
                int[] picks = new int[3];
                bool ok = true;
                for (int i = 0; i < 3 && ok; i++)
                {
                    picks[i] = PickOfType(currents[i], type);
                    if (picks[i] == 0) ok = false;
                }
                if (ok) return picks;
                break;
            }
        }

        // No restriction, or none that could be met.
        return [PickAny(currents[0]), PickAny(currents[1]), PickAny(currents[2])];
    }

    /// <summary>
    /// Picks a species that still has two evolutions ahead of it, so the starter grows the way a
    /// starter should.
    /// </summary>
    private int PickStarterWithEvolutions(SpeciesRandomizer speciesRand, int current)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int candidate = speciesRand.GetRandomSpecies(current);
            if (candidate > 0 && ChainLength(candidate) >= 3) return candidate;
        }
        return speciesRand.GetRandomSpecies(current);
    }

    /// <summary>How many stages a species' evolution line has, counting itself. Depth-capped.</summary>
    private int ChainLength(int species)
    {
        int best = 1;
        try
        {
            if (Config.Evolutions == null || species < 0 || species >= Config.Evolutions.Length) return 1;
            var set = Config.Evolutions[species];
            if (set?.PossibleEvolutions == null) return 1;

            foreach (var evo in set.PossibleEvolutions)
            {
                if (evo == null || evo.Method == 0 || evo.Species <= 0 || evo.Species == species) continue;
                if (evo.Species >= Config.Evolutions.Length) continue;

                // One level deeper only: three stages is the question being asked, and following
                // arbitrary depth risks a cycle on a randomized evolution table.
                int next = 1;
                var nextSet = Config.Evolutions[evo.Species];
                if (nextSet?.PossibleEvolutions != null &&
                    nextSet.PossibleEvolutions.Any(e => e != null && e.Method != 0 && e.Species > 0 && e.Species != evo.Species))
                    next = 2;

                best = Math.Max(best, 1 + next);
            }
        }
        catch { return 1; }
        return best;
    }

    /// <summary>
    /// The Pokemon offered and asked for by the in-game trades.
    /// </summary>
    private void RandomizeTrades(SpeciesRandomizer speciesRand)
    {
        const int stride = 0x34;
        try
        {
            var g = Config.GetGARCData("encounterstatic");
            if (g?.Files == null || g.Files.Length <= 4) return;

            byte[] data = g.Files[4];
            if (data == null || data.Length < stride) return;

            int count = data.Length / stride;
            for (int i = 0; i < count; i++)
            {
                int offset = i * stride;
                if (offset + stride > data.Length) break;

                int given = BitConverter.ToUInt16(data, offset);
                if (given > 0 && given <= Config.MaxSpeciesID)
                {
                    int newGiven = speciesRand.GetRandomSpecies(given);
                    if (newGiven > 0 && newGiven <= Config.MaxSpeciesID)
                    {
                        BitConverter.GetBytes((ushort)newGiven).CopyTo(data, offset);
                        data[offset + 4] = 0;   // base forme
                    }
                }

                // Mode 1 changes only what you receive; mode 2 also changes what is asked for.
                if (Settings.TradesMode < 2) continue;

                int wanted = BitConverter.ToUInt16(data, offset + 0x2C);
                if (wanted <= 0 || wanted > Config.MaxSpeciesID) continue;

                int newWanted = speciesRand.GetRandomSpecies(wanted);
                if (newWanted > 0 && newWanted <= Config.MaxSpeciesID)
                    BitConverter.GetBytes((ushort)newWanted).CopyTo(data, offset + 0x2C);
            }

            g.Files[4] = data;
            g.Save();
        }
        catch
        {
            // As above: a malformed or missing trade file must not take the run down.
        }
    }

    /// <summary>
    /// Items in the ability Pickup's loot table.
    /// </summary>
    private void RandomizePickup()
    {
        const int columns = 10;
        const int stride = columns + 2;

        try
        {
            var g = Config.GetGARCData("pickup");
            if (g?.Files == null || g.Files.Length == 0) return;

            byte[] data = g.Files[0];
            if (data == null || data.Length < 4) return;

            int rows = BitConverter.ToUInt16(data, 0) - 1;
            if (rows <= 0) return;

            var itemNames = Config.GetText(TextName.ItemNames);
            int maxItem = Config.Info.MaxItemID > 0 ? Config.Info.MaxItemID : 800;
            if (itemNames != null && itemNames.Length > 0)
                maxItem = Math.Min(maxItem, itemNames.Length - 1);
            if (maxItem < 1) return;

            // Pickup can hand this out from the first route, so no Z-Crystals here.
            var zFilter = new ZCrystalFilter(itemNames);

            bool Usable(int id)
            {
                if (Settings.PickupBanBadItems && Array.IndexOf(PickupBannedItems, id) >= 0) return false;
                if (zFilter.IsZCrystal(id)) return false;
                if (itemNames == null || id >= itemNames.Length) return true;
                string nm = itemNames[id]?.Trim();
                return !string.IsNullOrEmpty(nm) && nm is not ("???" or "-----");
            }

            for (int i = 0; i < rows; i++)
            {
                int offset = 4 + (i * stride);
                if (offset + 2 > data.Length) break;

                int item = 0;
                // Bounded rather than do/while: if a ROM has almost nothing nameable left, an
                // unbounded loop here would hang the whole randomization.
                for (int tries = 0; tries < 256; tries++)
                {
                    int candidate = (int)(Util.Random32() % (uint)maxItem) + 1;
                    if (!Usable(candidate)) continue;
                    item = candidate;
                    break;
                }
                if (item == 0) continue;   // leave the original entry alone

                BitConverter.GetBytes((ushort)item).CopyTo(data, offset);
            }

            g.Files[0] = data;
            g.Save();
        }
        catch
        {
            // A missing or short pickup GARC must not take the whole randomization down with it;
            // every other step has already written by this point.
        }
    }

    /// <summary>
    /// Items that make no sense as Pickup loot, mirroring <see cref="MartRandomizer"/>'s list.
    /// </summary>
    private static readonly int[] PickupBannedItems =
        [0x1B, 0x4B, 0x4C, 0x4D, 0x12, 0x121, 0x122, 0x123, 0x124];

    private static int GenerateSeed(string seedStr)
    {
        if (string.IsNullOrWhiteSpace(seedStr))
            return Environment.TickCount;
            
        if (int.TryParse(seedStr, out int numSeed))
            return numSeed;
            
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(seedStr));
        return BitConverter.ToInt32(hash, 0);
    }

    private static void ApplyFullTMCompatibility(PersonalInfo[] table)
    {
        for (int i = 1; i < table.Length; i++)
        {
            var tmhm = table[i].TMHM;
            if (tmhm == null)
                continue;

            for (int j = 0; j < Math.Min(100, tmhm.Length); j++)
                tmhm[j] = true;

            table[i].TMHM = tmhm;
        }
    }

    private static void ApplyFullHMCompatibility(PersonalInfo[] table)
    {
        for (int i = 1; i < table.Length; i++)
        {
            var tmhm = table[i].TMHM;
            if (tmhm == null || tmhm.Length <= 100)
                continue;

            for (int j = 100; j < tmhm.Length; j++)
                tmhm[j] = true;

            table[i].TMHM = tmhm;
        }
    }

    private static void ApplyFullTutorCompatibility(PersonalInfo[] table)
    {
        for (int i = 1; i < table.Length; i++)
        {
            if (table[i] is pk3DS.Core.Structures.PersonalInfo.PersonalInfoSM sm)
            {
                for (int j = 0; j < sm.TutorFlags.Length; j++)
                    sm.TutorFlags[j] = true;
                continue;
            }

            if (table[i].SpecialTutors == null)
                continue;

            foreach (var tutorGroup in table[i].SpecialTutors)
            {
                if (tutorGroup == null)
                    continue;

                for (int j = 0; j < tutorGroup.Length; j++)
                    tutorGroup[j] = true;
            }
        }
    }

    /// <summary>
    /// Drops Rare Candy's price and puts it in every ordinary Poké Mart.
    /// </summary>
    private string ApplyCheapRareCandies()
    {
        try
        {
            var names = Config.GetText(TextName.ItemNames);
            int candy = Array.FindIndex(names, n => string.Equals(n?.Trim(), "Rare Candy", StringComparison.OrdinalIgnoreCase));
            if (candy <= 0) return "WARNING: Rare Candy was not found in the item names, so nothing was changed.";

            // --- price ---
            int price = Math.Max(0, Settings.CheapRareCandyPrice);
            var itemGarc = Config.GetGARCData("item");
            var files = itemGarc.Files;
            if (candy >= files.Length) return $"WARNING: Rare Candy (id {candy}) is outside the item table.";

            var record = new pk3DS.Core.Structures.Item(files[candy]) { BuyPrice = price };
            files[candy] = record.Write();
            itemGarc.Save();

            // --- stocking ---
            string croPath = Path.Combine(RomFSPath, "Shop.cro");
            if (!File.Exists(croPath))
                return $"Rare Candy now costs {price}. Shop.cro was not found, so no mart was stocked.";

            byte[] cro = File.ReadAllBytes(croPath);
            var layout = MartLayout.Read(cro);
            if (!layout.Valid)
                return $"Rare Candy now costs {price}. Shop.cro's shop table could not be read, so no mart was stocked.";

            var lists = new List<int[]>();
            int stocked = 0, already = 0;
            for (int i = 0; i < layout.ShopOffsets.Length; i++)
            {
                var current = new List<int>();
                for (int k = 0; k < layout.ShopCounts[i]; k++)
                {
                    int o = layout.ShopOffsets[i] + (k * 2);
                    if (o + 1 < cro.Length) current.Add(BitConverter.ToUInt16(cro, o));
                }

                if (current.Contains(candy)) already++;
                else { current.Add(candy); stocked++; }

                lists.Add([.. current]);
            }

            byte[] rebuilt = MartLayout.Rebuild(cro, lists, [], out _);
            var after = MartLayout.Read(rebuilt);
            if (!after.Valid || after.Validate(rebuilt).Count > 0)
                return $"Rare Candy now costs {price}, but the rewritten Shop.cro did not validate, so it was not saved.";

            pk3DS.Core.CTR.CROUtil.SaveCro(croPath, rebuilt);
            return $"Rare Candy now costs {price} and is stocked in {stocked} mart(s) ({already} already sold it).";
        }
        catch (Exception ex)
        {
            return $"WARNING: cheap Rare Candies could not be applied ({ex.GetType().Name}: {ex.Message}).";
        }
    }

    /// <summary>
    /// Installs the story level caps chosen on the Level Caps tab.
    /// </summary>
    /// <summary>
    /// Installs the battle mechanic patches the randomizer offers, and reports each one.
    /// </summary>
    private List<string> InstallSelectedRecipes()
    {
        var log = new List<string>();
        try
        {
            string version = pk3DS.Core.Modding.Research.ResearchVersion.Resolve(Config);
            string battleCro = Path.Combine(RomFSPath ?? Config?.RomFS ?? "", "Battle.cro");
            if (!File.Exists(battleCro))
            {
                log.Add("WARNING: Battle.cro was not found, so no patches were installed.");
                return log;
            }

            var db = pk3DS.Core.Modding.Research.ResearchDatabase.LoadEmbedded(version);
            var map = pk3DS.Core.Modding.Research.BattleMechanicMap.Build(File.ReadAllBytes(battleCro), db, battleCro);
            var book = pk3DS.Core.Modding.Research.Recipes.Discover(db, version);

            int done = 0, skipped = 0;
            bool warnedAboutSpace = false;
            foreach (string name in Settings.InstallPatches)
            {
                var recipe = book.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (recipe == null)
                {
                    log.Add($"WARNING: '{name}' is not available on this build and was skipped.");
                    skipped++;
                    continue;
                }

                // Ids for anything that claims a slot. Shared with the Research Center so an
                // unattended install places things exactly where the editor would have.
                var problems = new List<string>();
                var values = pk3DS.Core.Modding.Research.RecipeIdAllocator.AssignIds(recipe, Config, problems);
                if (problems.Count > 0)
                {
                    log.Add($"WARNING: '{name}' was skipped - {problems[0]}.");
                    skipped++;
                    continue;
                }

                var plan = pk3DS.Core.Modding.Research.RecipeInstaller.Plan(recipe, Config, battleCro, db, map, values);
                if (!plan.Ok)
                {
                    log.Add($"WARNING: '{name}' was refused: " +
                            (plan.Describe().Split('\n').LastOrDefault(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase))?.Trim()
                             ?? "see the Research Center for details"));
                    skipped++;
                    continue;
                }

                var applied = pk3DS.Core.Modding.Research.RecipeInstaller.Apply(recipe, Config, battleCro, db, map, values);
                if (applied.Ok) done++;
                else
                {
                    skipped++;
                    var errors = applied.Describe().Split('\n')
                        .Where(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                        .Select(l => l.Replace("ERROR:", "").Trim())
                        .Where(l => l.Length > 0)
                        .ToList();
                    string why = errors.FirstOrDefault();
                    log.Add($"WARNING: '{name}' was not installed" + (why != null ? $": {why}" : "."));

                    bool spaceProblem = why != null &&
                        (why.Contains("reserve exhausted", StringComparison.OrdinalIgnoreCase) ||
                         why.Contains("structural verification", StringComparison.OrdinalIgnoreCase));
                    if (spaceProblem && !warnedAboutSpace)
                    {
                        warnedAboutSpace = true;
                        log.Add("NOTE: Battle.cro is out of room for more item packages (a stock one fits about four). " +
                                "Installing them one at a time does not help - expanding its code segment does.");
                    }
                }

                // The map describes the binary as it was, so it is rebuilt between recipes; the
                // next one has to see what the last one wrote.
                if (applied.Ok && File.Exists(battleCro))
                    map = pk3DS.Core.Modding.Research.BattleMechanicMap.Build(File.ReadAllBytes(battleCro), db, battleCro);
            }

            log.Add($"Patches installed: {done} of {Settings.InstallPatches.Count}" +
                    (skipped > 0 ? $" ({skipped} skipped)" : "") + ".");
        }
        catch (Exception ex)
        {
            log.Add($"WARNING: patches could not be installed ({ex.GetType().Name}: {ex.Message}).");
        }
        return log;
    }

    /// <summary>
    /// Dynamically scales battle EXP yield in code.bin by hooking the loaded species EXP formula.
    /// </summary>
    public static bool ApplyExpMultiplier(GameConfig config, int multiplier, List<string> log)
    {
        if (config == null || multiplier < 1 || multiplier > 255) return false;
        string codePath = pk3DS.Core.CTR.ExeFS.ResolveCodeBin(config.ExeFS);
        if (string.IsNullOrEmpty(codePath) || !File.Exists(codePath))
        {
            log?.Add("WARNING: ExeFS/code.bin not found; EXP multiplier patch skipped.");
            return false;
        }

        string v = pk3DS.Core.Modding.Research.ResearchVersion.Resolve(config);
        int hookOfs;
        byte[] branchBytes;
        if (v == "US")
        {
            hookOfs = 0x3AB188;
            branchBytes = [0x32, 0x3A, 0x04, 0xEB]; // bl #0x4B9A58
        }
        else if (v == "UM")
        {
            hookOfs = 0x3AB190;
            branchBytes = [0x30, 0x3A, 0x04, 0xEB]; // bl #0x4B9A58
        }
        else
        {
            log?.Add($"WARNING: EXP Multiplier patch is not supported for {v}.");
            return false;
        }

        int payloadOfs = 0x4B9A58;
        byte multByte = (byte)multiplier;
        byte[] payloadBytes = [
            0xB2, 0x02, 0xD0, 0xE1,     // ldrh r0, [r0, #0x2b2]
            0x02, 0x40, 0x2D, 0xE9,     // push {r1, lr}
            multByte, 0x10, 0xA0, 0xE3, // mov r1, #multiplier
            0x90, 0x01, 0x00, 0xE0,     // mul r0, r0, r1
            0x02, 0x80, 0xBD, 0xE8      // pop {r1, pc}
        ];

        byte[] code = File.ReadAllBytes(codePath);
        if (hookOfs + 4 > code.Length || payloadOfs + payloadBytes.Length > code.Length)
        {
            log?.Add("WARNING: code.bin too small for EXP multiplier patch.");
            return false;
        }

        Array.Copy(branchBytes, 0, code, hookOfs, 4);
        Array.Copy(payloadBytes, 0, code, payloadOfs, payloadBytes.Length);
        File.WriteAllBytes(codePath, code);
        log?.Add($"EXP Multiplier: {multiplier}x applied to code.bin.");
        return true;
    }

    /// <summary>
    /// Rewrites the 18x18 type chart in code.bin, and says what happened.
    /// </summary>
    private string InstallTypeEffectiveness()
    {
        try
        {
            string codePath = pk3DS.Core.CTR.ExeFS.ResolveCodeBin(Config?.ExeFS);
            if (!File.Exists(codePath))
                return "WARNING: code.bin was not found, so the type chart was not changed.";

            byte[] code = File.ReadAllBytes(codePath);
            int offset = TypeEffectivenessTable.Locate(code);
            if (offset < 0)
                return "WARNING: the type chart could not be found in code.bin, so it was not changed.";

            var original = TypeEffectivenessTable.Read(code, offset);
            var log = new List<string>();
            var updated = TypeEffectivenessRandomizer.Apply(
                original, Settings.TypeEffectivenessMode,
                Settings.TypeEffectivenessAddRandomImmunities, log);

            var problems = updated.Validate();
            if (problems.Count > 0)
                return $"WARNING: the new type chart was rejected ({problems[0]}); nothing was changed.";

            updated.WriteTo(code, offset);
            File.WriteAllBytes(codePath, code);

            return log.Count > 0 ? log[0] : "Type chart rewritten.";
        }
        catch (Exception ex)
        {
            return $"WARNING: the type chart could not be changed ({ex.GetType().Name}: {ex.Message}).";
        }
    }

    private string InstallLevelCaps()
    {
        try
        {
            string croPath = Path.Combine(RomFSPath, "Battle.cro");
            string codePath = pk3DS.Core.CTR.ExeFS.ResolveCodeBin(Config?.ExeFS);

            if (!File.Exists(croPath))
                return "WARNING: Battle.cro was not found, so level caps were not installed.";

            byte[] cro = File.ReadAllBytes(croPath);
            byte[] code = File.Exists(codePath) ? File.ReadAllBytes(codePath) : null;

            bool ultraMoon = string.Equals(
                pk3DS.Core.Modding.Research.ResearchVersion.Resolve(Config), "UM", StringComparison.OrdinalIgnoreCase);

            var stock = pk3DS.Core.Modding.Research.LevelCapTable.Default(ultraMoon);
            pk3DS.Core.Modding.Research.LevelCapTable table;

            if (Settings.LevelCapCaps is { Count: > 0 } caps && caps.Count == stock.Entries.Count)
            {
                for (int i = 0; i < caps.Count; i++)
                    stock.Entries[i] = stock.Entries[i] with { Cap = (byte)Math.Clamp(caps[i], 1, pk3DS.Core.Modding.Research.LevelCapTable.HardCeiling) };
                table = stock;
            }
            else if (Settings.LevelCapMatchTrainers)
            {
                var levels = pk3DS.Core.Modding.Research.TrainerLevelSampler.Collect(Config);
                table = pk3DS.Core.Modding.Research.LevelCapPresets.BuildFromTrainerLevels(
                    levels, (byte)Settings.LevelCapFinal, ultraMoon);
            }
            else
            {
                table = pk3DS.Core.Modding.Research.LevelCapPresets.Build(Settings.LevelCapShift, (byte)Settings.LevelCapFinal, ultraMoon);
            }
            pk3DS.Core.Modding.Research.LevelCapSettings.Table = table;

            if (code != null && !pk3DS.Core.Modding.Research.CodeSpaceBudget.Measure(code).Fits(pk3DS.Core.Modding.Research.CodeSpaceBudget.LevelCapBytes))
            {
                return "WARNING: " + pk3DS.Core.Modding.Research.CodeSpaceBudget.ExplainShortfall(
                    code, pk3DS.Core.Modding.Research.CodeSpaceBudget.LevelCapBytes, "Level caps");
            }

            var sites = pk3DS.Core.Modding.Research.LevelCapPatch.Install(cro, code, table);
            if (sites.Count == 0)
                return "WARNING: no level cap hook site was found, so nothing was installed.";

            pk3DS.Core.CTR.CROUtil.SaveCro(croPath, cro);
            if (code != null) File.WriteAllBytes(codePath, code);

            return $"Level caps installed: {sites.Count} site(s), {table.Entries.Count} checkpoints, final cap {Settings.LevelCapFinal}.";
        }
        catch (Exception ex)
        {
            return $"WARNING: level caps could not be installed ({ex.GetType().Name}: {ex.Message}).";
        }
    }

    private ushort[] RandomizeTMMoveTable()
    {
        if (string.IsNullOrWhiteSpace(Config.ExeFS) || !Directory.Exists(Config.ExeFS))
            return null;

        string[] files = Directory.GetFiles(Config.ExeFS);
        if (files.Length == 0)
            return null;

        string codeFile = files.FirstOrDefault(file => File.Exists(file) && Path.GetFileNameWithoutExtension(file).Contains("code", StringComparison.OrdinalIgnoreCase));
        if (codeFile == null)
            return null;

        byte[] code = File.ReadAllBytes(codeFile);
        if (code.Length < 0x100000)
            return null;

        int count = DetectTMCount(code);
        if (count <= 0)
            count = 100;

        bool expansionPackActive = Settings.ExpandTMs && Config.MaxSpeciesID >= 1025;
        if (expansionPackActive && count < 128)
            count = 128;

        if (!Settings.ExpandTMs && count > 100)
            count = 100;

        int offset = DetectTMOffset(code);
        if (offset < 0)
            return null;

        ushort[] defaultMoves = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            int moveOffset = GetTMOffset(code, offset, i);
            if (moveOffset >= 0 && moveOffset + 1 < code.Length)
                defaultMoves[i] = BitConverter.ToUInt16(code, moveOffset);
        }

        ushort[] tmlist = pk3DS.Core.Modding.ResearchEngine.GetTMMoveArray(code, count, defaultMoves);

        if (Settings.TMHMMovesMode > 0)
        {
            var moveNames = Config.GetText(TextName.MoveNames);
            int maxMoveID = Config.Info.MaxMoveID;

            List<int> validMoves;
            if (Settings.CompetitiveRandomizer)
            {
                validMoves = Enumerable.Range(1, Math.Min(maxMoveID, moveNames.Length - 1))
                    .Where(m => !Legal.Z_Moves.Contains(m) &&
                                moveNames[m] != "—" && moveNames[m] != "———" &&
                                (Competitive.CompetitiveDatabase.CompetitiveMoves.Contains(moveNames[m]) ||
                                 Competitive.CompetitiveDatabase.SituationalMoves.Contains(moveNames[m])))
                    .ToList();
            }
            else
            {
                validMoves = Enumerable.Range(1, Math.Min(maxMoveID, moveNames.Length - 1))
                    .Where(m => !Legal.Z_Moves.Contains(m) && moveNames[m] != "—" && moveNames[m] != "———")
                    .ToList();

                if (Settings.TMHMMovesMode == 2)
                {
                    // Exclude game-breaking moves (OHKO, Spore, Swagger, Dark Void, etc.)
                    HashSet<int> banned = [12, 32, 90, 105, 166, 329, 462, 479];
                    validMoves = validMoves.Where(m => !banned.Contains(m)).ToList();
                }
            }

            if (validMoves.Count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    tmlist[i] = (ushort)validMoves[Util.Rand.Next(validMoves.Count)];
                }
            }
        }
        else
        {
            Util.Shuffle(tmlist);
        }

        if (expansionPackActive && Settings.TMHMMovesMode == 0)
        {
            var moveNamesForNewSlots = Config.GetText(TextName.MoveNames);
            int maxMoveIdForNewSlots = Config.Info.MaxMoveID;
            var validMovesForNewSlots = Enumerable.Range(1, Math.Min(maxMoveIdForNewSlots, moveNamesForNewSlots.Length - 1))
                .Where(m => !Legal.Z_Moves.Contains(m) && moveNamesForNewSlots[m] != "—" && moveNamesForNewSlots[m] != "———")
                .ToList();
            if (validMovesForNewSlots.Count > 0)
            {
                for (int i = 100; i < tmlist.Length && i < 128; i++)
                    tmlist[i] = (ushort)validMovesForNewSlots[Util.Rand.Next(validMovesForNewSlots.Count)];
            }
        }

        if (count <= 100)
        {
            for (int i = 0; i < count; i++)
            {
                int moveOffset = GetTMOffset(code, offset, i);
                if (moveOffset >= 0 && moveOffset + 1 < code.Length)
                    BitConverter.GetBytes(tmlist[i]).CopyTo(code, moveOffset);
            }

            File.WriteAllBytes(codeFile, code);
            return tmlist;
        }

        ushort[] defaultItems = GetDefaultTMItems();
        ushort[] itemlist = pk3DS.Core.Modding.ResearchEngine.GetTMItemArray(code, count, defaultItems);

        if (expansionPackActive)
        {
            pk3DS.Core.Modding.Research.ExpandedTMItems.AssignTo(itemlist, ExpansionPackNewTMItemStartId);

            int highestNewItemId = pk3DS.Core.Modding.Research.ExpandedTMItems.HighestId(ExpansionPackNewTMItemStartId);
            LastItemCeilingResult = pk3DS.Core.Modding.Research.ItemCeilingPatcher.Raise(code, highestNewItemId);

            // The expanded icon lookup sends every id at or above 1024 to icon 768, which is the
            // blank slot; without this the new TMs appear in the bag with no icon.
            LastTMIconResult = pk3DS.Core.Modding.Research.TMIconPatcher.Retarget(code);
        }

        pk3DS.Core.Modding.ResearchEngine.ApplyExpandedTMCodePatch(code, tmlist, itemlist);
        File.WriteAllBytes(codeFile, code);

        if (expansionPackActive)
            CreateExpansionPackTMItems(itemlist, tmlist);

        return tmlist;
    }

    public ushort ExpansionPackNewTMItemStartId { get; set; } = 1024;

    /// <summary>
    /// Outcome of the last item-ceiling widening, so callers can report exactly which bounds
    /// checks were raised (and to what) rather than having it happen invisibly.
    /// </summary>
    public pk3DS.Core.Modding.Research.ItemCeilingResult LastItemCeilingResult { get; private set; }

    /// <summary>Outcome of the last TM icon-fallback retarget, for the same reporting reason.</summary>
    public pk3DS.Core.Modding.Research.TMIconPatcher.Result LastTMIconResult { get; private set; }

    private void CreateExpansionPackTMItems(ushort[] itemlist, ushort[] tmlist)
    {
        if (string.IsNullOrWhiteSpace(Config.RomFS) || !Directory.Exists(Config.RomFS)) return;

        var newItemIds = new List<ushort>();
        for (int i = 100; i < itemlist.Length && i < 128; i++)
        {
            if (itemlist[i] > 0) newItemIds.Add(itemlist[i]);
        }
        if (newItemIds.Count == 0) return;

        int maxItemID = newItemIds.Max();
        ushort[] newItemArr = newItemIds.ToArray();

        pk3DS.Core.Modding.ResearchEngine.ApplyExpandedTMItemAttributesPatch(Config.RomFS, maxItemID, newItemArr);
        pk3DS.Core.Modding.ResearchEngine.ApplyExpandedTMBattleBagPatch(Config.RomFS, maxItemID, newItemArr);

        string[] itemNames = Config.GetText(TextName.ItemNames);
        if (maxItemID >= itemNames.Length)
        {
            int oldLen = itemNames.Length;
            Array.Resize(ref itemNames, maxItemID + 1);
            for (int i = oldLen; i < itemNames.Length; i++)
                itemNames[i] ??= "???";
        }

        string[] itemDescriptions = Config.GetText(TextName.ItemFlavor);
        if (maxItemID >= itemDescriptions.Length)
        {
            int oldLen = itemDescriptions.Length;
            Array.Resize(ref itemDescriptions, maxItemID + 1);
            for (int i = oldLen; i < itemDescriptions.Length; i++)
                itemDescriptions[i] ??= "???";
        }
        string[] moveDescriptions = Config.GetText(TextName.MoveFlavor);

        for (int i = 100; i < itemlist.Length && i < 128; i++)
        {
            int itemId = itemlist[i];
            if (itemId <= 0 || itemId >= itemNames.Length) continue;
            itemNames[itemId] = $"TM{(i + 1):D3}";
            if (itemId < itemDescriptions.Length && i < tmlist.Length && tmlist[i] > 0 && tmlist[i] < moveDescriptions.Length)
                itemDescriptions[itemId] = moveDescriptions[tmlist[i]];
        }

        Config.SetText(TextName.ItemNames, itemNames);
        Config.SetText(TextName.ItemFlavor, itemDescriptions);
        Config.SaveText(TextName.ItemNames);
        Config.SaveText(TextName.ItemFlavor);
    }

    private int DetectTMOffset(byte[] code)
    {
        byte[] tmSig = [0x0E, 0x02, 0x51, 0x01, 0xD9, 0x01];
        int foundOfs = pk3DS.Core.Util.IndexOfBytes(code, tmSig, 0x100000, 0);
        if (foundOfs >= 0)
            return foundOfs;

        byte[] signature = [0x03, 0x40, 0x03, 0x41, 0x03, 0x42, 0x03, 0x43, 0x03];
        int fallback = pk3DS.Core.Util.IndexOfBytes(code, signature, 0x400000, 0);
        if (fallback >= 0)
        {
            fallback += signature.Length;
            if (Config.USUM)
                fallback += 0x22;
            return fallback;
        }

        return -1;
    }

    private static int GetTMOffset(byte[] code, int offset, int index)
    {
        if (index < 100)
            return offset + (2 * index);

        if (index >= 107 && offset < 0x100000 && code.Length > 0x4BB794 + 2)
            return 0x4BB794 + (2 * (index - 107));

        return offset + (2 * index);
    }

    private int DetectTMCount(byte[] codeData)
    {
        byte[] customSig = [0x10, 0x40, 0x2D, 0xE9, 0x00, 0x00, 0x50, 0xE3, 0x0C, 0x40, 0x9F, 0x35, 0x00, 0x00, 0xA0, 0x23];
        byte[] mask = [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        int customOfs = pk3DS.Core.Modding.ResearchEngine.IndexOfBytesMasked(codeData, customSig, mask, 0);

        if (customOfs >= 0)
            return codeData[customOfs + 4];

        int offset = DetectTMOffset(codeData);
        if (offset < 0)
            return 0;

        int searchStart = Math.Max(0, offset - 0x2000);
        int searchEnd = Math.Min(codeData.Length - 4, offset + 0x2000);

        for (int i = searchStart; i < searchEnd; i += 4)
        {
            uint word = BitConverter.ToUInt32(codeData, i);
            if (word == 0xE3500064)
                return 100;
            if (word == 0xE350005B)
                return 128;
        }

        int bestCount = 0;
        for (int i = searchStart; i < searchEnd; i += 4)
        {
            uint word = BitConverter.ToUInt32(codeData, i);
            if ((word & 0xFFF00000) != 0xE3500000) continue;

            uint imm8 = word & 0xFF;
            uint rot = (word >> 8) & 0xF;
            uint value = rot == 0 ? imm8 : (imm8 >> (int)(rot * 2)) | (imm8 << (int)(32 - rot * 2));

            if (value > 100 && value <= 128 && (int)value > bestCount)
                bestCount = (int)value;
        }
        return bestCount > 0 ? bestCount : 100;
    }

    private static ushort[] GetDefaultTMItems()
    {
        ushort[] items = new ushort[107];
        for (int i = 0; i < 92; i++) items[i] = (ushort)(328 + i);
        for (int i = 92; i < 95; i++) items[i] = (ushort)(618 + (i - 92));
        for (int i = 95; i < 100; i++) items[i] = (ushort)(690 + (i - 95));
        for (int i = 100; i < 106; i++) items[i] = (ushort)(420 + (i - 100));
        items[106] = 737;
        return items;
    }

    // Finds which ability slot (1, 2, or 3=Hidden) holds the given ability ID on this species,
    // or 0 if none of its slots do.
    private static int FindAbilitySlot(PersonalInfo pi, int abilityId)
    {
        if (pi?.Abilities == null || abilityId <= 0) return 0;
        for (int i = 0; i < pi.Abilities.Length && i < 3; i++)
        {
            if (pi.Abilities[i] == abilityId) return i + 1;
        }
        return 0;
    }

    private ushort GetFinalEvolution(int species)
    {
        if (species <= 0) return 1;
        var finalEvos = Config?.Generation == 6 ? Legal.FinalEvolutions_6 : Legal.FinalEvolutions_7;
        if (finalEvos.Contains(species)) return (ushort)species;

        if (Config?.Evolutions != null && species < Config.Evolutions.Length)
        {
            int current = species;
            var visited = new HashSet<int> { current };

            while (current < Config.Evolutions.Length)
            {
                if (finalEvos.Contains(current)) return (ushort)current;

                var evoSet = Config.Evolutions[current];
                if (evoSet?.PossibleEvolutions == null) break;

                var evo = evoSet.PossibleEvolutions.FirstOrDefault(e => e.Species > 0 && e.Species != current && e.Method > 0);
                if (evo == null || visited.Contains(evo.Species)) break;

                current = evo.Species;
                visited.Add(current);
            }
            if (finalEvos.Contains(current)) return (ushort)current;
            if (current > 0 && current != species) return (ushort)current;
        }

        // Deterministic fallback: map to a final evolution species from the game's final evolution list
        int idx = Math.Abs(species * 37) % finalEvos.Length;
        return (ushort)finalEvos[idx];
    }
}
