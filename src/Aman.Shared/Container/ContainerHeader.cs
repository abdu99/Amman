using Aman.Shared.Branding;

namespace Aman.Shared.Container;

/// <summary>
/// Plaintext header of an AMAN container. Nothing here is secret (the KDF
/// salt/iterations/verifier are all meant to be public per standard
/// password-hash practice); confidentiality comes entirely from the
/// AES-256-GCM encrypted video payload that follows the header in the file.
/// </summary>
public sealed class ContainerHeader
{
    public required Guid PackageId { get; init; }
    public required ContainerConstants.HeaderFlags Flags { get; set; }

    public required byte[] KdfSalt { get; init; }
    public required int KdfIterations { get; init; }
    public required byte[] PasswordVerifier { get; init; } // 32 bytes

    public DateTime? ExpiresUtc { get; init; }
    public int? MaxRuns { get; init; }

    public byte[] VendorPublicKey { get; init; } = Array.Empty<byte>(); // SPKI, present iff RequireActivationCode
    public byte[] HeaderSignature { get; set; } = Array.Empty<byte>();  // ECDSA over everything above, present iff HasVendorSignature

    public required BrandingInfo Branding { get; init; }
    public required List<VideoEntryMeta> Entries { get; init; }

    /// <summary>Serializes everything except <see cref="HeaderSignature"/> — this is exactly what gets signed/verified.</summary>
    public byte[] ToSignablePrefixBytes()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write(ContainerConstants.HeaderMagic);
        w.Write(ContainerConstants.FormatVersion);
        w.Write(PackageId.ToByteArray());
        w.Write((byte)Flags);

        w.Write(KdfSalt.Length);
        w.Write(KdfSalt);
        w.Write(KdfIterations);
        w.Write(PasswordVerifier);

        w.Write(ExpiresUtc.HasValue);
        if (ExpiresUtc.HasValue) w.Write(ExpiresUtc.Value.Ticks);
        w.Write(MaxRuns.HasValue);
        if (MaxRuns.HasValue) w.Write(MaxRuns.Value);

        w.Write(VendorPublicKey.Length);
        w.Write(VendorPublicKey);

        Branding.WriteTo(w);

        w.Write(Entries.Count);
        foreach (var entry in Entries)
            entry.WriteTo(w);

        return ms.ToArray();
    }

    public byte[] ToBytes()
    {
        byte[] prefix = ToSignablePrefixBytes();
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(prefix);
        w.Write(HeaderSignature.Length);
        w.Write(HeaderSignature);
        return ms.ToArray();
    }

    public static ContainerHeader ReadFrom(BinaryReader r)
    {
        byte[] magic = r.ReadBytes(ContainerConstants.HeaderMagic.Length);
        if (!magic.AsSpan().SequenceEqual(ContainerConstants.HeaderMagic))
            throw new InvalidDataException("Not an AMAN container (bad magic).");

        ushort version = r.ReadUInt16();
        if (version != ContainerConstants.FormatVersion)
            throw new NotSupportedException($"Unsupported AMAN container version {version}.");

        var packageId = new Guid(r.ReadBytes(16));
        var flags = (ContainerConstants.HeaderFlags)r.ReadByte();

        int saltLen = r.ReadInt32();
        byte[] salt = r.ReadBytes(saltLen);
        int iterations = r.ReadInt32();
        byte[] verifier = r.ReadBytes(32);

        DateTime? expiresUtc = r.ReadBoolean() ? new DateTime(r.ReadInt64(), DateTimeKind.Utc) : null;
        int? maxRuns = r.ReadBoolean() ? r.ReadInt32() : null;

        int vendorKeyLen = r.ReadInt32();
        byte[] vendorKey = r.ReadBytes(vendorKeyLen);

        var branding = BrandingInfo.ReadFrom(r);

        int entryCount = r.ReadInt32();
        var entries = new List<VideoEntryMeta>(entryCount);
        for (int i = 0; i < entryCount; i++)
            entries.Add(VideoEntryMeta.ReadFrom(r));

        int sigLen = r.ReadInt32();
        byte[] signature = r.ReadBytes(sigLen);

        return new ContainerHeader
        {
            PackageId = packageId,
            Flags = flags,
            KdfSalt = salt,
            KdfIterations = iterations,
            PasswordVerifier = verifier,
            ExpiresUtc = expiresUtc,
            MaxRuns = maxRuns,
            VendorPublicKey = vendorKey,
            HeaderSignature = signature,
            Branding = branding,
            Entries = entries,
        };
    }
}
