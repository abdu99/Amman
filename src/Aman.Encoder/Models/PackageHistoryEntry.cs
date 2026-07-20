using System;

namespace Aman.Encoder.Models;

/// <summary>One row of the local "which packages have I built" log — lets the
/// seller come back days/weeks later and generate an activation code for a
/// package without having to have manually written down its Package ID.</summary>
public sealed class PackageHistoryEntry
{
    public required Guid PackageId { get; init; }
    public required string Title { get; init; }
    public required DateTime BuiltUtc { get; init; }
    public required string OutputPath { get; init; }
    public required bool RequiresActivationCode { get; init; }

    /// <summary>Non-null if this package exceeded the single-.exe size limit and was built as
    /// Player.exe + a sidecar .aman data file — the two must be distributed together.</summary>
    public string? SidecarPath { get; init; }
}
