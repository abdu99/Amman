using System.Diagnostics;
using System.IO;

namespace Aman.Shared.Container;

/// <summary>
/// Finds the AMAN container blob appended after the Player.exe stub (or,
/// for a standalone .aman file, the blob that *is* the whole file) by
/// reading the fixed 16-byte trailer at end-of-file.
/// </summary>
public static class SelfExeLocator
{
    public static string GetCurrentExecutablePath()
        => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;

    public static (long BlobStart, long BlobLength) Locate(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        if (fs.Length < ContainerConstants.TrailerFooterSize)
            throw new InvalidDataException("File is too small to contain an AMAN package.");

        fs.Seek(-ContainerConstants.TrailerFooterSize, SeekOrigin.End);
        byte[] footer = new byte[ContainerConstants.TrailerFooterSize];
        fs.ReadExactly(footer);

        if (!footer.AsSpan(8, 8).SequenceEqual(ContainerConstants.TrailerMagic))
            throw new InvalidDataException("No AMAN package trailer found — file was not built by Aman.Encoder.");

        long blobLength = BitConverter.ToInt64(footer, 0);
        long blobStart = fs.Length - ContainerConstants.TrailerFooterSize - blobLength;

        if (blobStart < 0 || blobLength <= 0)
            throw new InvalidDataException("AMAN package trailer is corrupt.");

        return (blobStart, blobLength);
    }
}
