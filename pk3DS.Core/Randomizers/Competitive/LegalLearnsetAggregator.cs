using System;
using System.Collections.Generic;
using System.Linq;
using pk3DS.Core.Structures;
using pk3DS.Core.Structures.PersonalInfo;

namespace pk3DS.Core.Randomizers.Competitive;

public class LegalLearnsetAggregator
{
    private readonly GameConfig Config;
    private readonly Dictionary<int, HashSet<int>> LegalMovePools = new();
    private string[] CachedMoveNames;

    public LegalLearnsetAggregator(GameConfig config)
    {
        Config = config;
        BuildMovePools();
    }

    private void BuildMovePools()
    {
        if (Config?.Personal == null) return;
        CachedMoveNames = Config.GetText(TextName.MoveNames);

        int speciesCount = Config.MaxSpeciesID;
        for (int species = 1; species <= speciesCount; species++)
        {
            HashSet<int> moves = new();

            // 1. Level-Up Moves (excluding Z-moves)
            if (Config.Learnsets != null && species < Config.Learnsets.Length)
            {
                var learnset = Config.Learnsets[species];
                if (learnset?.Moves != null)
                {
                    foreach (int m in learnset.Moves)
                    {
                        if (m > 0 && !Legal.Z_Moves.Contains(m)) moves.Add(m);
                    }
                }
            }

            // 2. TM/HM Moves via PersonalInfo.TMHM bitflags
            var pi = Config.Personal[species];
            if (pi?.TMHM != null)
            {
            }

            // 3. Type Tutors
            if (pi?.TypeTutors != null)
            {
                // Same mapping issue as TMs — tutor flags need a move ID lookup table.
                // For now, the level-up learnset is the primary data source.
            }

            // 4. Special Tutors (ORAS/USUM tutor islands)
            if (pi?.SpecialTutors != null)
            {
                foreach (var tutorSet in pi.SpecialTutors)
                {
                    // Each bool[] maps to a tutor move list — without the ID array, we skip.
                }
            }

            LegalMovePools[species] = moves;
        }
    }

    /// <summary>
    /// Injects TM move IDs into all species' legal move pools using the provided TM move list.
    /// Call this after the TM randomizer has finalized TM assignments.
    /// </summary>
    public void InjectTMMoves(ushort[] tmMoveIDs)
    {
        if (tmMoveIDs == null || Config?.Personal == null) return;

        int speciesCount = Config.MaxSpeciesID;
        for (int species = 1; species <= speciesCount; species++)
        {
            var pi = Config.Personal[species];
            if (pi?.TMHM == null) continue;
            if (!LegalMovePools.TryGetValue(species, out var pool)) continue;

            for (int t = 0; t < pi.TMHM.Length && t < tmMoveIDs.Length; t++)
            {
                if (pi.TMHM[t] && tmMoveIDs[t] > 0 && !Legal.Z_Moves.Contains(tmMoveIDs[t]))
                    pool.Add(tmMoveIDs[t]);
            }
        }
    }

    /// <summary>
    /// Injects tutor move IDs into all species' legal move pools.
    /// </summary>
    public void InjectTutorMoves(ushort[][] tutorMoveLists)
    {
        if (tutorMoveLists == null || Config?.Personal == null) return;

        int speciesCount = Config.MaxSpeciesID;
        for (int species = 1; species <= speciesCount; species++)
        {
            var pi = Config.Personal[species];
            if (pi == null) continue;
            if (!LegalMovePools.TryGetValue(species, out var pool)) continue;

            // Type tutors
            if (pi.TypeTutors != null && tutorMoveLists.Length > 0 && tutorMoveLists[0] != null)
            {
                var typeTutorMoves = tutorMoveLists[0];
                for (int t = 0; t < pi.TypeTutors.Length && t < typeTutorMoves.Length; t++)
                {
                    if (pi.TypeTutors[t] && typeTutorMoves[t] > 0 && !Legal.Z_Moves.Contains(typeTutorMoves[t]))
                        pool.Add(typeTutorMoves[t]);
                }
            }

            // Special tutors (index 1+ in tutorMoveLists maps to SpecialTutors[0+])
            if (pi.SpecialTutors != null)
            {
                for (int s = 0; s < pi.SpecialTutors.Length; s++)
                {
                    int listIdx = s + 1;
                    if (listIdx >= tutorMoveLists.Length || tutorMoveLists[listIdx] == null) continue;
                    var tutorMoves = tutorMoveLists[listIdx];
                    var flags = pi.SpecialTutors[s];
                    for (int t = 0; t < flags.Length && t < tutorMoves.Length; t++)
                    {
                        if (flags[t] && tutorMoves[t] > 0 && !Legal.Z_Moves.Contains(tutorMoves[t]))
                            pool.Add(tutorMoves[t]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns the complete set of legal move IDs for a given species.
    /// </summary>
    public HashSet<int> GetLegalMoves(int species)
    {
        return LegalMovePools.TryGetValue(species, out var set) ? set : new HashSet<int>();
    }

    /// <summary>
    /// Returns the complete set of legal move names for a given species.
    /// </summary>
    public HashSet<string> GetLegalMoveNames(int species)
    {
        var ids = GetLegalMoves(species);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (CachedMoveNames == null || CachedMoveNames.Length == 0) return names;

        foreach (int id in ids)
        {
            if (id > 0 && id < CachedMoveNames.Length)
            {
                string n = CachedMoveNames[id];
                if (!string.IsNullOrEmpty(n)) names.Add(n);
            }
        }
        return names;
    }

    /// <summary>
    /// Checks if a species legally learns a specific move by ID.
    /// </summary>
    public bool LearnsMove(int species, int moveId)
    {
        return LegalMovePools.TryGetValue(species, out var set) && set.Contains(moveId);
    }

    /// <summary>
    /// Checks if a species learns any move from the given name set.
    /// </summary>
    public bool LearnsAnyOf(int species, HashSet<string> moveNames)
    {
        if (moveNames == null || moveNames.Count == 0) return false;
        var legalNames = GetLegalMoveNames(species);
        return legalNames.Overlaps(moveNames);
    }

    /// <summary>
    /// Counts how many moves from the given category set the species can learn.
    /// </summary>
    public int CountMovesInCategory(int species, HashSet<string> categoryNames)
    {
        if (categoryNames == null || categoryNames.Count == 0) return 0;
        var legalNames = GetLegalMoveNames(species);
        return legalNames.Count(n => categoryNames.Contains(n));
    }

    /// <summary>
    /// Counts how many moves from the given category set are also STAB for the given types.
    /// </summary>
    public int CountSTABMovesInCategory(int species, HashSet<string> categoryNames, int[] types)
    {
        if (categoryNames == null || types == null) return 0;
        var legalNames = GetLegalMoveNames(species);
        var moveData = Config?.Moves;
        if (CachedMoveNames == null || moveData == null) return 0;

        int count = 0;
        var ids = GetLegalMoves(species);
        foreach (int id in ids)
        {
            if (id <= 0 || id >= CachedMoveNames.Length || id >= moveData.Length) continue;
            string name = CachedMoveNames[id];
            if (string.IsNullOrEmpty(name) || !categoryNames.Contains(name)) continue;
            int moveType = moveData[id].Type;
            if (Array.IndexOf(types, moveType) >= 0) count++;
        }
        return count;
    }

    /// <summary>
    /// Resolves a move name to its ID using cached names.
    /// Returns -1 if not found.
    /// </summary>
    public int GetMoveId(string moveName)
    {
        if (CachedMoveNames == null || string.IsNullOrEmpty(moveName)) return -1;
        for (int i = 0; i < CachedMoveNames.Length; i++)
        {
            if (moveName.Equals(CachedMoveNames[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Checks if a species is the final stage of its evolutionary line.
    /// </summary>
    public bool IsFinalStage(int species)
    {
        if (Config?.Evolutions == null || species <= 0 || species >= Config.Evolutions.Length)
            return true; // Default to true if no evo data
        var evoSet = Config.Evolutions[species];
        if (evoSet?.PossibleEvolutions == null) return true;
        return !evoSet.PossibleEvolutions.Any(e => e.Species > 0 && e.Method > 0);
    }

    /// <summary>
    /// Gets all pre-evolution species IDs for a given species (walks the evo chain backwards).
    /// </summary>
    public List<int> GetPreEvolutions(int species)
    {
        var result = new List<int>();
        if (Config?.Evolutions == null) return result;

        for (int s = 1; s < Config.Evolutions.Length; s++)
        {
            var evoSet = Config.Evolutions[s];
            if (evoSet?.PossibleEvolutions == null) continue;
            if (evoSet.PossibleEvolutions.Any(e => e.Species == species && e.Method > 0))
                result.Add(s);
        }
        return result;
    }
}
