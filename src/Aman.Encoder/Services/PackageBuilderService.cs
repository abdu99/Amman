using System.IO;
using System.Threading;
using Aman.Encoder.Models;
using Aman.Shared.Container;
using Aman.Shared.Licensing;

namespace Aman.Encoder.Services;

public sealed class PackageBuilderService
{
    /// <summary>
    /// Windows won't run a Portable Executable larger than ~4 GiB (a hard OS
    /// limit, not a .NET one). Leave a wide safety margin below that for
    /// header/branding/signature overhead — real overhead here is at most a
    /// few hundred KB, so this margin is generous, not tight.
    /// </summary>
    public const long SingleExeSizeLimit = 3_900_000_000;

    /// <summary>
    /// Player.exe is a separately-built project (src/Aman.Player), published
    /// self-contained/single-file and copied into Resources/PlayerStub by
    /// build/publish-player.ps1. Aman.Encoder never compiles Player itself —
    /// it just appends encrypted data after the stub's existing bytes (or,
    /// for oversized packages, writes it to a sidecar file next to a plain
    /// copy of the stub — see BuildAsync).
    /// </summary>
    public static string LocatePlayerStub()
    {
        string candidate = Path.Combine(AppContext.BaseDirectory, "Resources", "PlayerStub", "Player.exe");
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                "Player.exe stub not found. Build Aman.Player first (build/publish-player.ps1), " +
                "which copies it into Resources/PlayerStub next to Aman.Encoder.exe.",
                candidate);
        }
        return candidate;
    }

    /// <summary>Rough total output size — used to decide single-file vs. sidecar mode before building.</summary>
    public static long EstimatePackageSize(ContainerBuildRequest request)
    {
        long stubLength = new FileInfo(LocatePlayerStub()).Length;
        long videoTotal = 0;
        foreach (var video in request.Videos)
            videoTotal += new FileInfo(video.FilePath).Length;

        return stubLength + videoTotal + 1_000_000; // +1 MB pad for header/branding/signature overhead
    }

    public async Task<PackageBuildResult> BuildAsync(ContainerBuildRequest request, VendorIdentity? signer, string outputExePath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string stubPath = LocatePlayerStub();
        long stubLength = new FileInfo(stubPath).Length;

        string? dir = Path.GetDirectoryName(outputExePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        bool useSidecar = EstimatePackageSize(request) > SingleExeSizeLimit;

        return useSidecar
            ? await BuildWithSidecarAsync(request, signer, stubPath, outputExePath, progress, ct)
            : await BuildSingleExeAsync(request, signer, stubPath, stubLength, outputExePath, progress, ct);
    }

    private static async Task<PackageBuildResult> BuildSingleExeAsync(ContainerBuildRequest request, VendorIdentity? signer,
        string stubPath, long stubLength, string outputExePath, IProgress<double>? progress, CancellationToken ct)
    {
        try
        {
            await using (var output = new FileStream(outputExePath, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufferSize: 1 << 20, useAsync: true))
            {
                await using (var stub = new FileStream(stubPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    await stub.CopyToAsync(output, ct);

                if (output.Position != stubLength)
                {
                    throw new IOException(
                        $"Only {output.Position:N0} of {stubLength:N0} bytes of the player stub were written " +
                        $"to '{outputExePath}' — the disk may be full or the destination unwritable.");
                }

                await ContainerWriter.BuildAsync(request, signer, output, progress, ct);
            }
        }
        catch
        {
            // A partially-written .exe left at the destination looks like a real, working
            // package but isn't — delete it rather than let a failed export masquerade as one.
            try { File.Delete(outputExePath); } catch { /* best effort cleanup */ }
            throw;
        }

        return new PackageBuildResult { ExePath = outputExePath, ExeSizeBytes = new FileInfo(outputExePath).Length };
    }

    private static async Task<PackageBuildResult> BuildWithSidecarAsync(ContainerBuildRequest request, VendorIdentity? signer,
        string stubPath, string outputExePath, IProgress<double>? progress, CancellationToken ct)
    {
        string sidecarPath = Path.ChangeExtension(outputExePath, ".aman");

        try
        {
            await Task.Run(() => File.Copy(stubPath, outputExePath, overwrite: true), ct);

            await using (var output = new FileStream(sidecarPath, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufferSize: 1 << 20, useAsync: true))
                await ContainerWriter.BuildAsync(request, signer, output, progress, ct);
        }
        catch
        {
            try { File.Delete(outputExePath); } catch { /* best effort cleanup */ }
            try { File.Delete(sidecarPath); } catch { /* best effort cleanup */ }
            throw;
        }

        return new PackageBuildResult
        {
            ExePath = outputExePath,
            ExeSizeBytes = new FileInfo(outputExePath).Length,
            SidecarPath = sidecarPath,
            SidecarSizeBytes = new FileInfo(sidecarPath).Length,
        };
    }
}
