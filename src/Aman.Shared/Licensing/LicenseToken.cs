using System;
using System.IO;

namespace Aman.Shared.Licensing;

/// <summary>
/// The claims that get signed into an activation code / license file.
/// Everything here is plaintext (not secret) — its integrity, not its
/// confidentiality, is what the ECDSA signature protects.
/// </summary>
public sealed class LicenseToken
{
    public required Guid PackageId { get; init; }

    /// <summary>Empty = not hardware-locked (works on any machine that also knows the password).</summary>
    public string MachineId { get; init; } = string.Empty;

    public DateTime IssuedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>null = never expires.</summary>
    public DateTime? ExpiresUtc { get; init; }

    /// <summary>null = unlimited plays.</summary>
    public int? MaxRuns { get; init; }

    public byte[] ToSignableBytes()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(1); // token format version
        w.Write(PackageId.ToByteArray());
        w.Write(MachineId);
        w.Write(IssuedUtc.Ticks);
        w.Write(ExpiresUtc.HasValue);
        if (ExpiresUtc.HasValue) w.Write(ExpiresUtc.Value.Ticks);
        w.Write(MaxRuns.HasValue);
        if (MaxRuns.HasValue) w.Write(MaxRuns.Value);

        return ms.ToArray();
    }

    public static LicenseToken FromSignableBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var r = new BinaryReader(ms);

        int version = r.ReadInt32();
        if (version != 1)
            throw new NotSupportedException($"Unsupported license token version {version}.");

        var packageId = new Guid(r.ReadBytes(16));
        string machineId = r.ReadString();
        var issuedUtc = new DateTime(r.ReadInt64(), DateTimeKind.Utc);
        DateTime? expiresUtc = r.ReadBoolean() ? new DateTime(r.ReadInt64(), DateTimeKind.Utc) : null;
        int? maxRuns = r.ReadBoolean() ? r.ReadInt32() : null;

        return new LicenseToken
        {
            PackageId = packageId,
            MachineId = machineId,
            IssuedUtc = issuedUtc,
            ExpiresUtc = expiresUtc,
            MaxRuns = maxRuns,
        };
    }
}
