#nullable enable

namespace pk3DS.Core.Modding.Research;

/// <summary>
/// The level cap progression the installer will write, shared between the editor and the recipe.
/// </summary>
public static class LevelCapSettings
{
    private static LevelCapTable? _table;

    /// <summary>The progression to install. Defaults to the researched one until edited.</summary>
    public static LevelCapTable Table
    {
        get => _table ??= LevelCapTable.Default();
        set { _table = value; Customised = true; }
    }

    /// <summary>
    /// Whether the user has actually chosen a table, as opposed to inheriting the default.
    /// </summary>
    public static bool Customised { get; private set; }

    /// <summary>Forgets any edited table, so the researched progression applies again.</summary>
    public static void Reset() { _table = null; Customised = false; }
}
