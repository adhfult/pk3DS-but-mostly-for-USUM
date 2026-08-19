using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace pk3DS.Core.Modding.Research;

/// <summary>One timing slot of a ported mechanic: either new code, or a stock routine to reuse.</summary>
public sealed class PortedSlot
{
    /// <summary>Timing byte, as "0x9E".</summary>
    public string Timing { get; set; } = "0x00";

    /// <summary>Machine code, hex. Null when <see cref="Reuse"/> is set.</summary>
    public string Code { get; set; }

    /// <summary>Address <see cref="Code"/> was assembled for, as "0x1062F8".</summary>
    public string SourceBase { get; set; }

    /// <summary>
    /// A stock mechanic whose routine this slot borrows. Recorded by id rather than address so it
    /// re-resolves on a build where that routine has moved.
    /// </summary>
    public PortedReuse Reuse { get; set; }

    public byte TimingByte => (byte)Convert.ToInt32(Timing, 16);
    public uint SourceBaseValue => string.IsNullOrEmpty(SourceBase) ? 0 : Convert.ToUInt32(SourceBase, 16);
    public byte[] CodeBytes => string.IsNullOrEmpty(Code) ? null : Convert.FromHexString(Code);
}

/// <summary>Points at a stock routine by the mechanic that owns it.</summary>
public sealed class PortedReuse
{
    public string Kind { get; set; } = "";
    public uint Id { get; set; }
    /// <summary>Id as written, so a package can express it as "${donorId}". Wins over <see cref="Id"/>.</summary>
    public string IdText { get; set; }
    /// <summary>Which of that mechanic's slots, matched by timing. Empty means "the only one".</summary>
    public string Timing { get; set; }
    public string Name { get; set; }
}

/// <summary>A mechanic to (re)create on the target ROM.</summary>
public sealed class PortedMechanic
{
    public string Kind { get; set; } = "Move";
    public uint Id { get; set; }
    /// <summary>Id as written, so a package can express it as "${itemId}". Wins over <see cref="Id"/>.</summary>
    public string IdText { get; set; }
    public string Name { get; set; } = "";
    public List<PortedSlot> Slots { get; set; } = [];

    public CustomMechanicKind KindValue => Enum.Parse<CustomMechanicKind>(Kind, true);
}

/// <summary>
/// A patch applied at a fixed code address, recorded with the bytes that were there before.
/// <para>
/// The original is not decoration: it is the only way to tell whether the target build still has
/// the same code at that address. A hook written blind onto a shifted binary corrupts whatever
/// moved into its place, and does so silently.
/// </para>
/// </summary>
public sealed class PortedSitePatch
{
    public string Offset { get; set; } = "0x0";
    public string Original { get; set; } = "";
    public string Patched { get; set; } = "";

    /// <summary>
    /// True when <see cref="Patched"/> contains a branch into relocated space, so the bytes cannot
    /// be copied and must be re-encoded against the target's own reserve.
    /// </summary>
    public bool IsHook { get; set; }

    /// <summary>For a hook: where it should land, expressed as the ported routine's name.</summary>
    public string HookTarget { get; set; }

    public uint OffsetValue => Convert.ToUInt32(Offset, 16);
    public byte[] OriginalBytes => Convert.FromHexString(Original);
    public byte[] PatchedBytes => Convert.FromHexString(Patched);
}

/// <summary>
/// A free-standing block of code placed in the reserve, which hooks branch into.
/// <para>
/// Distinct from a mechanic's effect routine: nothing in a master table points here, so the block
/// only exists because some hook site jumps to it. It is carried by name and rebased on arrival.
/// </para>
/// </summary>
public sealed class PortedBlock
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    /// <summary>Address the bytes were assembled for, as "0x115154".</summary>
    public string SourceBase { get; set; } = "0x0";

    public byte[] CodeBytes => Convert.FromHexString(Code);
    public uint SourceBaseValue => Convert.ToUInt32(SourceBase, 16);
}

/// <summary>Everything needed to reproduce a set of edits on a different build of the same game.</summary>
public sealed class PortManifest
{
    public string Description { get; set; } = "";

    /// <summary>Mechanic entries to add to Battle.cro.</summary>
    [JsonPropertyName("battleCroMechanics")]
    public List<PortedMechanic> Mechanics { get; set; } = [];

    /// <summary>Hook bodies placed in the reserve; referenced by <see cref="PortedSitePatch.HookTarget"/>.</summary>
    [JsonPropertyName("battleCroBlocks")]
    public List<PortedBlock> Blocks { get; set; } = [];

    /// <summary>Fixed-address edits: NOPs, branch hooks, and bounds that are not table counts.</summary>
    [JsonPropertyName("battleCroSitePatches")]
    public List<PortedSitePatch> SitePatches { get; set; } = [];

    /// <summary>
    /// Edits to CROs other than Battle.cro, keyed by file name ("Bag.cro", "Shop.cro", ...).
    /// <para>
    /// These have no master tables and no id fingerprints, so everything is address-anchored and
    /// verified against recorded originals. Bag.cro is the one that matters today: the Nature Mint
    /// and Ability Capsule hooks live there, and without them the code.bin half has nothing calling
    /// into it — which is exactly why the mints did not survive the first port.
    /// </para>
    /// </summary>
    [JsonPropertyName("otherCros")]
    public Dictionary<string, CroSitePort> OtherCros { get; set; } = [];

    /// <summary>Offsets that are structural (hashes, header fields) and must never be copied.</summary>
    public static readonly uint[] StructuralOffsets = [0x000000, 0x0000B8, 0x00012C, 0x000090];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);
    public static PortManifest FromJson(string json) => JsonSerializer.Deserialize<PortManifest>(json, Options);
    public static PortManifest Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path) => File.WriteAllText(path, ToJson());

    /// <summary>Drops entries that describe CRO bookkeeping rather than an actual edit.</summary>
    public int RemoveStructural()
    {
        int before = SitePatches.Count;
        SitePatches = SitePatches
            .Where(p => !StructuralOffsets.Contains(p.OffsetValue) && p.OriginalBytes.Length <= 64)
            .ToList();
        return before - SitePatches.Count;
    }
}
