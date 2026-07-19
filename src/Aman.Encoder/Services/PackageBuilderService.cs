using System.IO;
using System.Threading;
using Aman.Shared.Container;
using Aman.Shared.Licensing;

namespace Aman.Encoder.Services;

public sealed class PackageBuilderService
{
    /// <summary>
    /// Player.exe is a separately-built project (src/Aman.Player), published
    /// self-contained/single-file and copied into Resources/PlayerStub by
    /// build/publish-player.ps1. Aman.Encoder never compiles Player itself —
    /// it just appends encrypted data after the stub's existing bytes.
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

    public async Task BuildAsync(ContainerBuildRequest request, VendorIdentity? signer, string outputExePath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string stubPath = LocatePlayerStub();

        string? dir = Path.GetDirectoryName(outputExePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using (var output = new FileStream(outputExePath, FileMode.Create, FileAccess.Write, FileShare.None,
                   bufferSize: 1 << 20, useAsync: true))
        {
            await using (var stub = new FileStream(stubPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                await stub.CopyToAsync(output, ct);

            await ContainerWriter.BuildAsync(request, signer, output, progress, ct);
        }
    }
}
