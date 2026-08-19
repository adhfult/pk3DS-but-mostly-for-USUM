using System;
using System.Collections.Generic;
using System.Linq;

namespace pk3DS.Core.Modding.Research;

/// <summary>Result of a space request.</summary>
public readonly record struct SpaceGrant(uint Offset, int Size, string Source)
{
    public bool Success => Offset != 0 && Size > 0;
    public static SpaceGrant Fail(string why) => new(0, 0, why);
}

/// <summary>
/// Hands out regions of a binary for new code or data.
/// <para>
/// Two sources are consulted, in order. First the researcher-maintained free-space registry
/// ("Table of my custom functions"), which records the offset, length and remaining room of every
/// region already carved out — this is authoritative, because it accounts for space that is
/// reserved-but-not-yet-filled and therefore looks free to a byte scan. Only if that yields
/// nothing does it fall back to scanning the file for runs of padding.
/// </para>
/// <para>
/// Grants are tracked for the lifetime of the allocator so two effects installed in one session
/// can never be handed the same bytes — the previous code path (a bare <c>FindFreeCodeSpace</c>
/// scan per call) would happily return the same offset twice.
/// </para>
/// </summary>
public sealed class CodeSpaceAllocator
{
    private readonly byte[] _binary;
    private readonly List<ResearchFreeSpace> _registry;
    private readonly List<(uint Start, uint End)> _granted = [];

    /// <summary>Bytes treated as padding when scanning: 0xCC traps and plain zero fill.</summary>
    private static readonly byte[] PaddingBytes = [0xCC, 0x00];

    public ResearchTarget Target { get; }
    public IReadOnlyList<(uint Start, uint End)> Granted => _granted;

    public CodeSpaceAllocator(byte[] binary, ResearchDatabase db, ResearchTarget target)
    {
        _binary = binary ?? throw new ArgumentNullException(nameof(binary));
        Target = target;
        _registry = db?.FreeSpaceFor(target) ?? [];
    }

    /// <summary>
    /// Reserves <paramref name="size"/> bytes, 4-byte aligned. Returns a failed grant (rather than
    /// throwing or silently returning 0) so callers can surface a precise reason.
    /// </summary>
    public SpaceGrant Allocate(int size, string purpose = null)
    {
        if (size <= 0) return SpaceGrant.Fail("requested size must be positive");
        int need = (size + 3) & ~3;

        foreach (var region in _registry)
        {
            // Room is measured from the END of whatever already occupies the region.
            uint start = region.Offset + (uint)Math.Max(0, region.Length);
            start = (start + 3u) & ~3u;
            if (region.Room < need) continue;
            if (start + need > _binary.Length) continue;
            if (Overlaps(start, (uint)need)) continue;

            _granted.Add((start, start + (uint)need));
            return new SpaceGrant(start, need, $"registry:{region.Function ?? region.Origin.ToString()}");
        }

        var scanned = ScanForPadding(need);
        if (scanned.Success) return scanned;

        return SpaceGrant.Fail(
            $"no free region of {need} bytes in {Target} " +
            $"(registry had {_registry.Count} candidate(s), largest room {(_registry.Count > 0 ? _registry.Max(r => r.Room) : 0)})");
    }

    /// <summary>Fallback: find a run of padding long enough, avoiding anything already granted.</summary>
    private SpaceGrant ScanForPadding(int need)
    {
        int run = 0;
        for (int i = 0; i < _binary.Length; i++)
        {
            bool pad = PaddingBytes.Contains(_binary[i]);
            if (!pad) { run = 0; continue; }

            run++;
            if (run < need) continue;

            uint start = (uint)(i - need + 1);
            start = (start + 3u) & ~3u;
            if (start + need > _binary.Length) { run = 0; continue; }
            if (Overlaps(start, (uint)need)) { run = 0; continue; }

            // Require the whole aligned window to still be padding after rounding.
            bool clean = true;
            for (uint j = start; j < start + need; j++)
                if (!PaddingBytes.Contains(_binary[j])) { clean = false; break; }
            if (!clean) { run = 0; continue; }

            _granted.Add((start, start + (uint)need));
            return new SpaceGrant(start, need, "scan:padding");
        }
        return SpaceGrant.Fail("no padding run found");
    }

    private bool Overlaps(uint start, uint size)
    {
        uint end = start + size;
        return _granted.Any(g => start < g.End && end > g.Start);
    }

    /// <summary>Total bytes the registry believes are available, for reporting.</summary>
    public int RegistryRoom => _registry.Sum(r => r.Room);
    public int RegistryRegions => _registry.Count;
}
