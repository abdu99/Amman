namespace Aman.Encoder.Models;

/// <summary>What <c>PackageBuilderService.BuildAsync</c> actually produced.</summary>
public sealed class PackageBuildResult
{
    public required string ExePath { get; init; }
    public required long ExeSizeBytes { get; init; }

    /// <summary>Non-null when the package exceeded the single-file size limit and was
    /// split into a small Player.exe plus this sidecar data file (kept together).</summary>
    public string? SidecarPath { get; init; }
    public long? SidecarSizeBytes { get; init; }

    public bool UsedSidecar => SidecarPath is not null;
}
