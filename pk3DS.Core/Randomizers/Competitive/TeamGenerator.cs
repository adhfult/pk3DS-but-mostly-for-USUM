using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Randomizers.Competitive;

public enum CompetitiveRole
{
    OffensiveSweeper,
    OffensiveBreaker,
    OffensivePivot,
    OffensiveCleaner,
    OffensiveLead,
    OffensiveUtility,
    SpeedControl,
    TankySweeper,
    TankyCleaner,
    BulkyAttacker,
    BulkySweeper,
    BulkyPivot,
    BulkyLead,
    BulkyUtility,
    DefensiveWall,
    DefensivePivot
}

public enum TeamArchetype
{
    HyperOffenseScreens = 1,
    HyperOffenseHazards = 2,
    BalancedOffense = 3,
    Sun = 4,
    Rain = 5,
    Snow = 6,
    Sand = 7,
    PsySpam = 8,
    Balance = 9,
    Offense = 10,
    Stall = 11,
    TrickRoom = 12,
    BatonPass = 13,
    SemiStall = 14,
    TypeSpam = 15,
    Fat = 16,
    TerrainTeam = 17
}

public class TeamArchetypeSpecification
{
    public TeamArchetype Archetype { get; set; }
    public string RequiredLeadAbility { get; set; } = "";
    public string RequiredLeadItem { get; set; } = "";
    public HashSet<string> RequiredMoveCategories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RequiredTeammateAbilities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<int> RequiredTypes { get; set; } = new();           // Types that must appear on the team
    public int MinPivotMoves { get; set; } = 0;                        // Minimum pivot move count across team
    public int MaxSpeed { get; set; } = int.MaxValue;                  // Max speed for all team members (Trick Room)
    public bool IsTrickRoom { get; set; } = false;
    public bool RequireHazardSetter { get; set; } = false;             // At least one hazard move on the team
    public int MinOffensiveStat { get; set; } = 0;                     // Min Atk or SpA for offensive members
    public HashSet<string> RequiredLeadMoves { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] AlternateLeadAbilities { get; set; } = Array.Empty<string>(); // e.g. Grassy Surge OR Electric Surge
}

public class TeamGenerator
{
    private readonly Random Rnd = Util.Rand;

    // Archetype weight system: higher weight = more likely to be selected.
    // Defaults to equal weight. Can be modified by UI sliders.
    public Dictionary<TeamArchetype, float> ArchetypeWeights { get; set; } = InitDefaultWeights();

    private static Dictionary<TeamArchetype, float> InitDefaultWeights()
    {
        var weights = new Dictionary<TeamArchetype, float>();
        foreach (TeamArchetype a in Enum.GetValues(typeof(TeamArchetype)))
            weights[a] = 1.0f;
        return weights;
    }

    /// <summary>
    /// Picks a weighted-random archetype.
    /// </summary>
    public TeamArchetype GetRandomArchetype()
    {
        float totalWeight = ArchetypeWeights.Values.Sum();
        if (totalWeight <= 0)
        {
            Array values = Enum.GetValues(typeof(TeamArchetype));
            return (TeamArchetype)values.GetValue(Rnd.Next(values.Length));
        }

        float roll = (float)(Rnd.NextDouble() * totalWeight);
        float cumulative = 0;
        foreach (var kvp in ArchetypeWeights)
        {
            cumulative += kvp.Value;
            if (roll <= cumulative) return kvp.Key;
        }
        return TeamArchetype.Balance; // Fallback
    }

    /// <summary>
    /// Returns the required roles for each member of a team for the given archetype.
    /// </summary>
    public CompetitiveRole[] GetRolesForArchetype(TeamArchetype archetype, int teamSize = 6)
    {
        if (teamSize <= 0) teamSize = 1;
        CompetitiveRole[] roles = new CompetitiveRole[teamSize];
        void SetRole(int idx, CompetitiveRole r) { if (idx >= 0 && idx < teamSize) roles[idx] = r; }

        switch (archetype)
        {
            // Structure 1: Hyper Offense with Screens
            case TeamArchetype.HyperOffenseScreens:
                SetRole(0, CompetitiveRole.OffensiveLead);
                for (int i = 1; i < teamSize - 1; i++) SetRole(i, CompetitiveRole.OffensiveSweeper);
                SetRole(teamSize - 1, CompetitiveRole.OffensiveCleaner);
                break;

            // Structure 2: Hyper Offense with Hazards
            case TeamArchetype.HyperOffenseHazards:
                SetRole(0, CompetitiveRole.OffensiveLead);
                SetRole(1, CompetitiveRole.OffensiveSweeper);
                SetRole(2, CompetitiveRole.OffensiveBreaker);
                for (int i = 3; i < teamSize - 1; i++) SetRole(i, CompetitiveRole.OffensiveSweeper);
                SetRole(teamSize - 1, CompetitiveRole.SpeedControl);
                break;

            // Structure 3: Balanced Offense
            case TeamArchetype.BalancedOffense:
                SetRole(0, CompetitiveRole.OffensivePivot);
                SetRole(1, CompetitiveRole.OffensiveSweeper);
                SetRole(2, CompetitiveRole.BulkyPivot);
                SetRole(3, CompetitiveRole.OffensiveBreaker);
                SetRole(4, CompetitiveRole.SpeedControl);
                SetRole(5, CompetitiveRole.BulkyAttacker);
                break;

            // Structure 4: Sun
            case TeamArchetype.Sun:
                SetRole(0, CompetitiveRole.BulkyLead);       // Drought + Heat Rock
                SetRole(1, CompetitiveRole.OffensiveSweeper); // Chlorophyll
                SetRole(2, CompetitiveRole.OffensiveBreaker); // Protosynthesis / Fire
                SetRole(3, CompetitiveRole.BulkyAttacker);    // Ground-type
                SetRole(4, CompetitiveRole.OffensiveUtility); // Hazard setter
                SetRole(5, CompetitiveRole.SpeedControl);     // Fairy-type
                break;

            // Structure 5: Rain
            case TeamArchetype.Rain:
                SetRole(0, CompetitiveRole.BulkyLead);       // Drizzle + Damp Rock
                SetRole(1, CompetitiveRole.OffensiveSweeper); // Swift Swim Water
                SetRole(2, CompetitiveRole.BulkyAttacker);    // Grass-type
                SetRole(3, CompetitiveRole.OffensiveBreaker); // Thunder/Hurricane
                SetRole(4, CompetitiveRole.OffensivePivot);   // Water pivot
                SetRole(5, CompetitiveRole.BulkyPivot);       // Coverage
                break;

            // Structure 6: Snow/Hail
            case TeamArchetype.Snow:
                SetRole(0, CompetitiveRole.BulkyLead);       // Snow Warning
                SetRole(1, CompetitiveRole.OffensiveSweeper); // Slush Rush Ice
                SetRole(2, CompetitiveRole.BulkyAttacker);    // Water-type
                SetRole(3, CompetitiveRole.OffensivePivot);
                SetRole(4, CompetitiveRole.BulkyPivot);
                SetRole(5, CompetitiveRole.SpeedControl);
                break;

            // Structure 7: Sand
            case TeamArchetype.Sand:
                SetRole(0, CompetitiveRole.BulkyLead);       // Sand Stream
                SetRole(1, CompetitiveRole.OffensiveSweeper); // Sand Rush
                SetRole(2, CompetitiveRole.BulkyAttacker);    // Steel-type
                SetRole(3, CompetitiveRole.BulkyAttacker);    // Ground-type
                SetRole(4, CompetitiveRole.OffensivePivot);
                SetRole(5, CompetitiveRole.BulkyPivot);
                break;

            // Structure 8: PsySpam
            case TeamArchetype.PsySpam:
                SetRole(0, CompetitiveRole.OffensiveLead);   // Psychic Surge / Hazard Lead
                SetRole(1, CompetitiveRole.OffensiveSweeper); // Unburden + Psychic Seed
                SetRole(2, CompetitiveRole.OffensiveBreaker); // Expanding Force STAB
                SetRole(3, CompetitiveRole.BulkyAttacker);    // Fighting-type
                SetRole(4, CompetitiveRole.SpeedControl);
                SetRole(5, CompetitiveRole.OffensivePivot);
                break;

            // Structure 9: Balance
            case TeamArchetype.Balance:
                SetRole(0, CompetitiveRole.BulkyLead);       // Stealth Rock
                SetRole(1, CompetitiveRole.OffensiveSweeper); // Win-condition
                SetRole(2, CompetitiveRole.BulkyPivot);
                SetRole(3, CompetitiveRole.BulkyAttacker);    // Steel/Fairy/Ground
                SetRole(4, CompetitiveRole.SpeedControl);
                SetRole(5, CompetitiveRole.DefensiveWall);
                break;

            // Structure 10: Offense
            case TeamArchetype.Offense:
                SetRole(0, CompetitiveRole.OffensivePivot);   // Momentum generator
                SetRole(1, CompetitiveRole.OffensiveSweeper);
                SetRole(2, CompetitiveRole.OffensiveBreaker);
                SetRole(3, CompetitiveRole.OffensiveCleaner);
                SetRole(4, CompetitiveRole.SpeedControl);
                SetRole(5, CompetitiveRole.OffensiveUtility);
                break;

            // Structure 11: Stall
            case TeamArchetype.Stall:
                SetRole(0, CompetitiveRole.BulkyLead);
                for (int i = 1; i < teamSize - 1; i++) SetRole(i, CompetitiveRole.DefensiveWall);
                SetRole(teamSize - 1, CompetitiveRole.DefensivePivot);
                break;

            // Structure 12: Trick Room
            case TeamArchetype.TrickRoom:
                SetRole(0, CompetitiveRole.BulkyLead);       // TR Setter + Focus Sash
                SetRole(1, CompetitiveRole.BulkyUtility);     // Secondary TR Setter
                for (int i = 2; i < teamSize; i++) SetRole(i, CompetitiveRole.TankySweeper);
                break;

            // Structure 13: Baton Pass
            case TeamArchetype.BatonPass:
                SetRole(0, CompetitiveRole.OffensiveLead);   // BP + Focus Sash + Speed Boost/Moody
                SetRole(1, CompetitiveRole.OffensiveSweeper); // Receives boosts
                SetRole(2, CompetitiveRole.OffensiveCleaner);
                SetRole(3, CompetitiveRole.TankySweeper);
                SetRole(4, CompetitiveRole.OffensivePivot);
                SetRole(5, CompetitiveRole.SpeedControl);
                break;

            // Structure 14: Semi-Stall
            case TeamArchetype.SemiStall:
                SetRole(0, CompetitiveRole.BulkyLead);
                for (int i = 1; i < teamSize - 1; i++) SetRole(i, CompetitiveRole.DefensiveWall);
                SetRole(teamSize - 1, CompetitiveRole.BulkySweeper);
                break;

            // Structure 15: Type Spam
            case TeamArchetype.TypeSpam:
                SetRole(0, CompetitiveRole.OffensivePivot);
                SetRole(1, CompetitiveRole.OffensiveSweeper);
                SetRole(2, CompetitiveRole.OffensiveBreaker);
                SetRole(3, CompetitiveRole.BulkyPivot);
                SetRole(4, CompetitiveRole.BulkyUtility);
                SetRole(5, CompetitiveRole.SpeedControl);
                break;

            // Structure 16: Fat
            case TeamArchetype.Fat:
                SetRole(0, CompetitiveRole.BulkyLead);
                for (int i = 1; i < teamSize - 2; i++) SetRole(i, CompetitiveRole.DefensiveWall);
                SetRole(teamSize - 2, CompetitiveRole.SpeedControl);
                SetRole(teamSize - 1, CompetitiveRole.BulkySweeper);
                break;

            // Structure 17: Terrain Team
            case TeamArchetype.TerrainTeam:
                SetRole(0, CompetitiveRole.BulkyLead);       // Electric/Grassy Surge + Terrain Extender
                SetRole(1, CompetitiveRole.OffensiveSweeper); // Unburden + Terrain Seed
                SetRole(2, CompetitiveRole.OffensiveBreaker);
                SetRole(3, CompetitiveRole.OffensivePivot);
                SetRole(4, CompetitiveRole.BulkyAttacker);
                SetRole(5, CompetitiveRole.SpeedControl);
                break;
        }

        return roles;
    }

    /// <summary>
    /// Returns the full archetype specification with all constraints.
    /// </summary>
    public TeamArchetypeSpecification GetSpecification(TeamArchetype archetype)
    {
        var spec = new TeamArchetypeSpecification { Archetype = archetype };

        switch (archetype)
        {
            case TeamArchetype.HyperOffenseScreens:
                spec.RequiredLeadMoves.Add("Light Screen");
                spec.RequiredLeadMoves.Add("Reflect");
                spec.RequiredLeadItem = "Light Clay";
                break;

            case TeamArchetype.HyperOffenseHazards:
                spec.RequireHazardSetter = true;
                spec.RequiredLeadMoves.Add("Stealth Rock"); // Or other hazard
                break;

            case TeamArchetype.BalancedOffense:
                spec.RequireHazardSetter = true;
                spec.RequiredTypes = new HashSet<int> { PokemonTypes.Ground, PokemonTypes.Flying };
                spec.MinPivotMoves = 1;
                break;

            case TeamArchetype.Sun:
                spec.RequiredLeadAbility = "Drought";
                spec.RequiredLeadItem = "Heat Rock";
                spec.RequiredTeammateAbilities.Add("Chlorophyll");
                spec.RequiredTeammateAbilities.Add("Protosynthesis");
                spec.RequiredTypes = new HashSet<int> { PokemonTypes.Ground, PokemonTypes.Fairy };
                spec.RequireHazardSetter = true;
                break;

            case TeamArchetype.Rain:
                spec.RequiredLeadAbility = "Drizzle";
                spec.RequiredLeadItem = "Damp Rock";
                spec.RequiredTeammateAbilities.Add("Swift Swim");
                spec.RequiredTypes = new HashSet<int> { PokemonTypes.Grass };
                spec.MinPivotMoves = 2;
                break;

            case TeamArchetype.Snow:
                spec.RequiredLeadAbility = "Snow Warning";
                // Light Clay if has Aurora Veil, else Icy Rock (handled by engine)
                spec.RequiredTeammateAbilities.Add("Slush Rush");
                spec.RequiredTypes = new HashSet<int> { PokemonTypes.Water };
                spec.MinPivotMoves = 2;
                break;

            case TeamArchetype.Sand:
                spec.RequiredLeadAbility = "Sand Stream";
                spec.RequiredTeammateAbilities.Add("Sand Rush");
                spec.RequiredTypes = new HashSet<int> { PokemonTypes.Ground, PokemonTypes.Steel };
                spec.MinPivotMoves = 2;
                break;

            case TeamArchetype.PsySpam:
                spec.RequiredTeammateAbilities.Add("Psychic Surge");
                spec.RequiredTeammateAbilities.Add("Unburden");
                spec.RequiredTypes = new HashSet<int> { PokemonTypes.Fighting };
                break;

            case TeamArchetype.Balance:
                spec.RequireHazardSetter = true;
                spec.RequiredMoveCategories.Add("Stealth Rock");
                spec.RequiredTypes = new HashSet<int>
                {
                    PokemonTypes.Steel, PokemonTypes.Fairy,
                    PokemonTypes.Ground // Flying immunity via Flying type or Levitate
                };
                break;

            case TeamArchetype.Offense:
                spec.RequireHazardSetter = true;
                break;

            case TeamArchetype.Stall:
                spec.RequireHazardSetter = true;
                // 2+ hazard moves on the team (handled by role assignments)
                break;

            case TeamArchetype.TrickRoom:
                spec.IsTrickRoom = true;
                spec.MaxSpeed = 70;
                spec.RequiredMoveCategories.Add("Trick Room");
                spec.RequiredLeadItem = "Focus Sash";
                break;

            case TeamArchetype.BatonPass:
                spec.RequiredLeadMoves.Add("Baton Pass");
                spec.RequiredLeadItem = "Focus Sash";
                break;

            case TeamArchetype.SemiStall:
                spec.RequireHazardSetter = true;
                break;

            case TeamArchetype.TypeSpam:
                // Dynamic: type spam is decided at runtime
                break;

            case TeamArchetype.Fat:
                spec.RequireHazardSetter = true;
                break;

            case TeamArchetype.TerrainTeam:
                spec.AlternateLeadAbilities = new[] { "Electric Surge", "Grassy Surge" };
                spec.RequiredLeadItem = "Terrain Extender";
                spec.RequiredTeammateAbilities.Add("Unburden");
                break;
        }

        return spec;
    }

    /// <summary>
    /// Determines if a Pokemon's stats are suitable for the given role and archetype constraints.
    /// Used for team composition filtering during species selection.
    /// </summary>
    public static bool IsStatSuitableForRole(
        Structures.PersonalInfo.PersonalInfo pi, CompetitiveRole role, TeamArchetypeSpecification spec = null)
    {
        if (pi == null) return false;

        int atk = pi.Stats[CompetitiveDatabase.ATK];
        int spa = pi.Stats[CompetitiveDatabase.SPA];
        int spe = pi.Stats[CompetitiveDatabase.SPE];
        int hp = pi.Stats[CompetitiveDatabase.HP];
        int def = pi.Stats[CompetitiveDatabase.DEF];
        int spd = pi.Stats[CompetitiveDatabase.SPD];

        // Trick Room: speed must be <= MaxSpeed
        if (spec != null && spec.IsTrickRoom && spe > spec.MaxSpeed) return false;

        switch (role)
        {
            case CompetitiveRole.OffensiveSweeper:
            case CompetitiveRole.OffensiveBreaker:
            case CompetitiveRole.OffensiveCleaner:
                return (atk >= 80 || spa >= 80) && spe >= 70;

            case CompetitiveRole.OffensivePivot:
                return (atk >= 70 || spa >= 70);

            case CompetitiveRole.OffensiveLead:
                return spe >= 80 || atk >= 80 || spa >= 80;

            case CompetitiveRole.OffensiveUtility:
                return atk >= 70 || spa >= 70;

            case CompetitiveRole.SpeedControl:
                return spe >= 70;

            case CompetitiveRole.TankySweeper:
            case CompetitiveRole.TankyCleaner:
                return hp >= 70 && (atk >= 80 || spa >= 80);

            case CompetitiveRole.BulkyAttacker:
                return hp >= 70 && (atk >= 70 || spa >= 70);

            case CompetitiveRole.BulkySweeper:
                return hp >= 80 && (atk >= 70 || spa >= 70);

            case CompetitiveRole.BulkyPivot:
            case CompetitiveRole.BulkyUtility:
            case CompetitiveRole.BulkyLead:
                return hp >= 70 && (def >= 70 || spd >= 70);

            case CompetitiveRole.DefensiveWall:
            case CompetitiveRole.DefensivePivot:
                return hp >= 80 && (def >= 80 || spd >= 80);

            default:
                return true;
        }
    }
}
