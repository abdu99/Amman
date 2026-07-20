using System.Diagnostics;
using System.IO;

namespace Aman.Shared.Container;

/// <summary>
/// Finds the AMAN container data for a running Player. Windows won't run a
/// Portable Executable larger than ~4 GiB, so packages whose video content
/// would push the .exe past that limit are built with the encrypted data in
/// a sidecar "&lt;name&gt;.aman" file next to a small Player.exe instead of
/// appended to it — see docs/CONTAINER_FORMAT.md. Smaller packages still
/// keep everything in the single .exe, found via the trailer at its
/// end-of-file, exactly as before.
/// </summary>
public static class SelfExeLocator
{
    public static string GetCurrentExecutablePath()
        => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;

    /// <summary>Reads the fixed 16-byte trailer at end-of-file, if present and valid.</summary>
    private static bool TryLocateAppended(string filePath, out long blobStart, out long blobLength)
    {
        blobStart = 0;
        blobLength = 0;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length < ContainerConstants.TrailerFooterSize) return false;

        fs.Seek(-ContainerConstants.TrailerFooterSize, SeekOrigin.End);
        byte[] footer = new byte[ContainerConstants.TrailerFooterSize];
        fs.ReadExactly(footer);

        if (!footer.AsSpan(8, 8).SequenceEqual(ContainerConstants.TrailerMagic)) return false;

        long candidateLength = BitConverter.ToInt64(footer, 0);
        long candidateStart = fs.Length - ContainerConstants.TrailerFooterSize - candidateLength;
        if (candidateStart < 0 || candidateLength <= 0) return false;

        blobStart = candidateStart;
        blobLength = candidateLength;
        return true;
    }

    /// <summary>
    /// Locates the container data for <paramref name="exePath"/>: either appended to
    /// that file itself, or — for packages too large for a single .exe — in a
    /// sidecar file with the same base name and a ".aman" extension next to it.
    /// </summary>
    public static (string DataFilePath, long BlobStart, long BlobLength) LocateContainer(string exePath)
    {
        if (TryLocateAppended(exePath, out long blobStart, out long blobLength))
            return (exePath, blobStart, blobLength);

        string sidecarPath = Path.ChangeExtension(exePath, ".aman");
        if (File.Exists(sidecarPath) && TryLocateAppended(sidecarPath, out long sidecarStart, out long sidecarLength))
            return (sidecarPath, sidecarStart, sidecarLength);

        throw new InvalidDataException(
            $"No AMAN package data found. This file was not built by Aman.Encoder, or its sidecar data file " +
            $"('{Path.GetFileName(sidecarPath)}') is missing — both files must be kept together in the same folder.");
    }
}
