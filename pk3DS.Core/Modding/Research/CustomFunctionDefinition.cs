using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace pk3DS.Core.Modding.Research;

/// <summary>Which master table a custom effect attaches to.</summary>
public enum CustomMechanicKind { Move = 0, Ability = 1, Item = 2 }

/// <summary>
/// A user-authored effect, in a form that is meant to be written and edited by hand.
/// <para>
/// Everything here is symbolic: the target is named or indexed, code is ARM assembly text, stock
/// routines are called by name, and storage is requested by name and size. Nothing carries a file
/// offset, because offsets are resolved at install time against the ROM actually being patched
/// and the free-space registry — so a definition stays valid across ROM revisions and across
/// re-installs that land the code somewhere new.
/// </para>
/// </summary>
public sealed class CustomFunctionDefinition
{
    /// <summary>Human-readable name; also the default label in the editor.</summary>
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    /// <summary>Which binary this installs into.</summary>
    public ResearchTarget Target { get; set; } = ResearchTarget.BattleCro;

    /// <summary>Move / Ability / Item.</summary>
    public CustomMechanicKind Mechanic { get; set; } = CustomMechanicKind.Ability;

    /// <summary>Index in the master table. Use -1 with <see cref="MechanicName"/> to resolve by name.</summary>
    public int MechanicIndex { get; set; } = -1;

    /// <summary>Optional name, resolved against the game's text tables when index is -1.</summary>
    public string MechanicName { get; set; }

    /// <summary>
    /// When the routine runs. Meanings come from the research timing table (0x00 = stat change
    /// HP, 0x0D = Flash Fire triggered, ...) rather than being hardcoded here.
    /// </summary>
    public byte Timing { get; set; }

    /// <summary>ARM assembly, one instruction per line. Assembled at install time.</summary>
    public List<string> Assembly { get; set; } = [];

    /// <summary>
    /// Alternative to <see cref="Assembly"/>: raw machine code as a hex string. Useful for pasting
    /// a routine straight out of a research sheet.
    /// </summary>
    public string HexCode { get; set; }

    /// <summary>Persistent storage this effect needs, allocated in BSS.</summary>
    public List<CustomStorageRequest> Storage { get; set; } = [];

    /// <summary>
    /// Existing routine to reuse instead of supplying code — the name is looked up in the
    /// research function index, so "call the vanilla Flash Fire handler" needs no address.
    /// </summary>
    public string ReuseFunctionNamed { get; set; }

    /// <summary>Whether the installer may relocate the master table if it has no spare entry.</summary>
    public bool AllowTableExpansion { get; set; } = true;

    /// <summary>
    /// Assembles supplied text rather than <see cref="Assembly"/>, used after symbol substitution
    /// has replaced named references with addresses.
    /// </summary>
    public byte[] ResolveCodeFrom(string assemblyText, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(assemblyText)) { error = "No assembly to build."; return null; }
        try
        {
            var bytes = pk3DS.Core.CTR.ARMCodec.Assemble(assemblyText);
            if (bytes == null || bytes.Length == 0) { error = "Assembly produced no output."; return null; }
            return bytes;
        }
        catch (Exception ex) { error = $"Assembly failed: {ex.Message}"; return null; }
    }

    /// <summary>Resolved machine code for this definition, or null when it can't be produced.</summary>
    public byte[] ResolveCode(out string error)
    {
        error = null;

        if (!string.IsNullOrWhiteSpace(HexCode))
        {
            string hex = HexCode.Replace(" ", "").Replace("-", "").Replace("\n", "").Replace("\r", "");
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex[2..];
            if (hex.Length == 0 || hex.Length % 2 != 0 || !hex.All(Uri.IsHexDigit))
            { error = "HexCode is not a valid hex byte string."; return null; }
            try { return Convert.FromHexString(hex); }
            catch (Exception ex) { error = $"HexCode could not be parsed: {ex.Message}"; return null; }
        }

        if (Assembly is { Count: > 0 })
        {
            string text = string.Join(Environment.NewLine, Assembly);
            try
            {
                var bytes = pk3DS.Core.CTR.ARMCodec.Assemble(text);
                if (bytes == null || bytes.Length == 0)
                { error = "Assembly produced no output."; return null; }
                return bytes;
            }
            catch (Exception ex) { error = $"Assembly failed: {ex.Message}"; return null; }
        }

        error = "Definition supplies neither Assembly, HexCode, nor ReuseFunctionNamed.";
        return null;
    }

    #region Serialization

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static CustomFunctionDefinition FromJson(string json) =>
        JsonSerializer.Deserialize<CustomFunctionDefinition>(json, Options);

    public static List<CustomFunctionDefinition> LoadFolder(string folder, IList<string> diagnostics = null)
    {
        var list = new List<CustomFunctionDefinition>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return list;

        foreach (string file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                string text = File.ReadAllText(file);
                // A file may hold one definition or an array of them.
                if (text.TrimStart().StartsWith("["))
                {
                    var many = JsonSerializer.Deserialize<List<CustomFunctionDefinition>>(text, Options);
                    if (many != null) list.AddRange(many);
                }
                else
                {
                    var one = FromJson(text);
                    if (one != null) list.Add(one);
                }
            }
            catch (Exception ex)
            {
                diagnostics?.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return list;
    }

    public void Save(string path) => File.WriteAllText(path, ToJson());

    #endregion
}

/// <summary>A named piece of persistent state, allocated in BSS at install time.</summary>
public sealed class CustomStorageRequest
{
    public string Name { get; set; } = "";
    /// <summary>Bytes per Pokemon when <see cref="PerPokemon"/>, else total bytes.</summary>
    public int Size { get; set; } = 1;
    /// <summary>
    /// True to allocate one slot per battle participant (24 slots, index 0x00-0x17), matching the
    /// battle-index convention the stock routines use.
    /// </summary>
    public bool PerPokemon { get; set; }
}
