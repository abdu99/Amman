namespace Aman.Shared.Container;

public static class ContainerConstants
{
    /// <summary>Marks the start of an AMAN container blob (also used as the file signature for standalone .aman files).</summary>
    public static readonly byte[] HeaderMagic = "AMANPKG1"u8.ToArray();

    /// <summary>Marks the fixed 16-byte footer appended after Player.exe when producing a self-executable package.</summary>
    public static readonly byte[] TrailerMagic = "AMANTAIL"u8.ToArray();

    public const int TrailerFooterSize = 16; // 8 bytes blob length + 8 bytes magic

    public const ushort FormatVersion = 1;

    public const int DefaultChunkSize = 1 * 1024 * 1024; // 1 MiB plaintext chunks

    [Flags]
    public enum HeaderFlags : byte
    {
        None = 0,
        RequireActivationCode = 1 << 0,
        HasExpiry = 1 << 1,
        HasMaxRuns = 1 << 2,
        HasVendorSignature = 1 << 3,
    }

    public enum ThemeMode : byte
    {
        Dark = 0,
        Light = 1,
        System = 2,
    }
}
