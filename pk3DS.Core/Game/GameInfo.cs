namespace pk3DS.Core;

public class GameInfo
{
    public int MaxSpeciesID { get; set; }
    public int MaxItemID { get; set; }
    public int MaxMoveID { get; set; }
    public ushort[] HeldItems { get; set; }
    public int MaxAbilityID { get; set; }

    public GameInfo(GameConfig gameConfig)
    {
        switch (gameConfig.Version)
        {
            case GameVersion.XY: LoadXY(); break;
            case GameVersion.ORASDEMO:
            case GameVersion.ORAS: LoadAO(); break;
            case GameVersion.SMDEMO:
            case GameVersion.SM: LoadSM(); break;
            case GameVersion.US:
            case GameVersion.UM:
            case GameVersion.USUM: LoadUSUM(); break;
        }
        RecalculateLimits(gameConfig);
    }

    public void RecalculateLimits(GameConfig config)
    {
        if (config == null) return;

        // Dynamic species recalculation from Personal table or text list
        if (pk3DS.Core.Modding.ProjectState.Instance.AppliedPatches.Contains("Pokemon+ Gen 9 Expansion Patch") || (config.Personal != null && config.Personal.Table != null && config.Personal.Table.Length >= 1025))
        {
            MaxSpeciesID = 1025;
        }

        // Dynamic move count recalculation from moves list
        if (config.Moves != null && config.Moves.Length > MaxMoveID)
        {
            MaxMoveID = config.Moves.Length - 1;
        }

        // Dynamic ability count recalculation from ability text list
        var abils = config.GetText(TextName.AbilityNames);
        if (abils != null && abils.Length > 0)
        {
            MaxAbilityID = abils.Length - 1;
        }

        // Dynamic item count recalculation from item text list
        var items = config.GetText(TextName.ItemNames);
        if (items != null && items.Length > 0)
        {
            MaxItemID = items.Length - 1;
        }

        // Check ExpansionConfig for custom overrides
        try
        {
            var cfg = pk3DS.Core.Modding.ExpansionConfig.Load(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ExpansionConfig.json"));
            if (cfg != null)
            {
                if (cfg.MaxAbilities > MaxAbilityID) MaxAbilityID = cfg.MaxAbilities;
                if (cfg.MaxSpecies > MaxSpeciesID) MaxSpeciesID = cfg.MaxSpecies;
                if (cfg.MaxMoves > MaxMoveID) MaxMoveID = cfg.MaxMoves;
                if (cfg.MaxItems > MaxItemID) MaxItemID = cfg.MaxItems;
            }
        }
        catch { }
    }


    private void LoadXY()
    {
        MaxSpeciesID = Legal.MaxSpeciesID_6;
        MaxMoveID = Legal.MaxMoveID_6_XY;
        MaxItemID = Legal.MaxItemID_6_XY;
        HeldItems = Legal.HeldItem_XY;
        MaxAbilityID = Legal.MaxAbilityID_6_XY;
    }

    private void LoadAO()
    {
        MaxSpeciesID = Legal.MaxSpeciesID_6;
        MaxMoveID = Legal.MaxMoveID_6_AO;
        MaxItemID = Legal.MaxItemID_6_AO;
        HeldItems = Legal.HeldItem_AO;
        MaxAbilityID = Legal.MaxAbilityID_6_AO;
    }

    private void LoadSM()
    {
        MaxSpeciesID = Legal.MaxSpeciesID_7_SM;
        MaxMoveID = Legal.MaxMoveID_7_SM;
        MaxItemID = Legal.MaxItemID_7_SM;
        HeldItems = Legal.HeldItems_SM;
        MaxAbilityID = Legal.MaxAbilityID_7_SM;
    }

    private void LoadUSUM()
    {
        MaxSpeciesID = Legal.MaxSpeciesID_7_USUM;
        MaxMoveID = Legal.MaxMoveID_7_USUM;
        MaxItemID = Legal.MaxItemID_7_USUM;
        HeldItems = Legal.HeldItems_USUM;
        MaxAbilityID = Legal.MaxAbilityID_7_USUM;
    }
}