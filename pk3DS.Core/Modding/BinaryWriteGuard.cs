#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace pk3DS.Core.Modding;

/// <summary>Details of a pending write to an executable binary, shown to the user for approval.</summary>
public sealed class BinaryWriteRequest
{
    /// <summary>Full path of the file about to be overwritten.</summary>
    public string Path { get; init; } = "";

    /// <summary>File name alone, for display.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Which operation wants the write, in plain language.</summary>
    public string Reason { get; init; } = "";

    /// <summary>What specifically changes - offsets, counts - when the caller can say.</summary>
    public string Detail { get; init; } = "";

    /// <summary>Byte count of the replacement.</summary>
    public int Length { get; init; }

    /// <summary>How many bytes differ from what is on disk, or -1 when not computed.</summary>
    public int ChangedBytes { get; init; } = -1;
}

/// <summary>
/// A checkpoint in front of every write to an executable binary (code.bin, *.cro).
/// <para>
/// These files are patched at hardcoded offsets derived from one specific build. Applied to a ROM
/// that does not match - a different region, revision, or an already-expanded Expansion Pack build -
/// the same write lands in the middle of unrelated code and produces a binary that loads and then
/// misbehaves later, well away from the edit. Several of these patches ran as a side effect of an
/// ordinary editor action, so the first sign of trouble was a broken game.
/// </para>
/// <para>
/// Nothing here decides whether a patch is correct. It only ensures a human sees the write, and that
/// the original bytes still exist afterwards.
/// </para>
/// </summary>
public static class BinaryWriteGuard
{
    /// <summary>
    /// When set, guarded writes must be approved before they happen. On by default: an unattended
    /// overwrite of an executable is the failure this exists to prevent.
    /// </summary>
    public static bool RequireApproval { get; set; } = true;

    /// <summary>Keeps a one-time ".orig" copy of each file before it is first modified.</summary>
    public static bool BackupBeforeWrite { get; set; } = true;

    /// <summary>
    /// Asks the user to approve a write. Set by the UI layer at startup. When approval is required
    /// and nothing has been assigned, guarded writes are refused rather than allowed through - a
    /// prompt that cannot be shown is not consent.
    /// </summary>
    public static Func<BinaryWriteRequest, bool>? ApprovalHandler { get; set; }

    private static readonly List<string> Log = [];
    private static readonly object Sync = new();

    /// <summary>Every guarded write attempt this session and what became of it.</summary>
    public static IReadOnlyList<string> History
    {
        get { lock (Sync) return [.. Log]; }
    }

    /// <summary>True when this path is an executable binary rather than game data.</summary>
    public static bool IsGuarded(string path)
    {
        string name = System.IO.Path.GetFileName(path);
        if (name.Equals("code.bin", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("exefs", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) return true;
        return System.IO.Path.GetExtension(path).Equals(".cro", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes <paramref name="data"/> to <paramref name="path"/> if the write is permitted.
    /// Returns false when it was refused, in which case the file on disk is untouched.
    /// </summary>
    public static bool TryWrite(string path, byte[] data, string reason, string detail = "")
    {
        if (string.IsNullOrEmpty(path) || data == null)
            return false;

        if (!IsGuarded(path))
        {
            File.WriteAllBytes(path, data);
            return true;
        }

        var request = new BinaryWriteRequest
        {
            Path = path,
            Reason = reason,
            Detail = detail,
            Length = data.Length,
            ChangedBytes = CountChangedBytes(path, data),
        };

        if (RequireApproval)
        {
            var handler = ApprovalHandler;
            if (handler == null)
            {
                Record(request, "REFUSED (no approval handler available)");
                return false;
            }
            if (!handler(request))
            {
                Record(request, "DECLINED by user");
                return false;
            }
        }

        if (BackupBeforeWrite)
            TryBackup(path);

        pk3DS.Core.CTR.CROUtil.SaveCro(path, data);
        Record(request, RequireApproval ? "APPROVED" : "written (approval disabled)");
        return true;
    }

    /// <summary>
    /// How many bytes differ from the current file, so the prompt can distinguish a targeted
    /// four-byte patch from something rewriting half the binary.
    /// </summary>
    private static int CountChangedBytes(string path, byte[] data)
    {
        try
        {
            if (!File.Exists(path)) return -1;
            byte[] existing = File.ReadAllBytes(path);
            int n = Math.Min(existing.Length, data.Length);
            int changed = Math.Abs(existing.Length - data.Length);
            for (int i = 0; i < n; i++)
            {
                if (existing[i] != data[i]) changed++;
            }
            return changed;
        }
        catch { return -1; }
    }

    /// <summary>Copies the untouched original aside once, so a bad patch is recoverable.</summary>
    private static void TryBackup(string path)
    {
        try
        {
            string backup = path + ".orig";
            if (!File.Exists(backup) && File.Exists(path))
                File.Copy(path, backup);
        }
        catch { /* a missing backup must not stop an approved write */ }
    }

    private static void Record(BinaryWriteRequest r, string outcome)
    {
        lock (Sync)
        {
            Log.Add($"[{DateTime.Now:HH:mm:ss}] {r.FileName}: {outcome} - {r.Reason}"
                    + (r.ChangedBytes >= 0 ? $" ({r.ChangedBytes} bytes differ)" : ""));
        }
    }
}
