using Aman.Shared.Crypto;
using Aman.Shared.Licensing;

namespace Aman.Shared.Container;

/// <summary>
/// Builds an AMAN container blob: [header][entry 0 chunk stream][entry 1 chunk stream]...[16-byte trailer].
/// The blob is self-contained and self-locating (via the trailer), so the
/// exact same writer output works both as a standalone .aman file and as
/// the tail appended to a Player.exe stub.
/// </summary>
public static class ContainerWriter
{
    public static async Task BuildAsync(ContainerBuildRequest request, VendorIdentity? signer, Stream output,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (request.RequireActivationCode && signer is null)
            throw new ArgumentException("A vendor signer is required when RequireActivationCode is set.");

        byte[] salt = KeyDerivation.GenerateSalt();
        byte[] kek = KeyDerivation.DeriveKek(request.Password, salt, request.KdfIterations);
        byte[] verifier = KeyDerivation.DeriveVerifier(request.Password, salt, request.KdfIterations);

        var entries = new List<VideoEntryMeta>();
        var contentKeys = new Dictionary<Guid, byte[]>();

        foreach (var video in request.Videos)
        {
            var fileInfo = new FileInfo(video.FilePath);
            byte[] contentKey = AesGcmCipher.GenerateKey();
            byte[] noncePrefix = AesGcmCipher.GenerateChunkNoncePrefix();
            byte[] wrappedKey = AesGcmCipher.EncryptSmall(kek, contentKey);

            var meta = new VideoEntryMeta
            {
                Id = video.Id,
                Title = video.Title,
                OriginalFileName = Path.GetFileName(video.FilePath),
                Extension = Path.GetExtension(video.FilePath),
                PlaintextLength = fileInfo.Length,
                ChunkSize = request.ChunkSize,
                EncryptedContentKeyBlob = wrappedKey,
                ChunkNoncePrefix = noncePrefix,
            };
            meta.DataLength = meta.PlaintextLength + (long)meta.ChunkCount * AesGcmCipher.TagSizeBytes;

            entries.Add(meta);
            contentKeys[video.Id] = contentKey;
        }

        var flags = ContainerConstants.HeaderFlags.None;
        if (request.RequireActivationCode) flags |= ContainerConstants.HeaderFlags.RequireActivationCode;
        if (request.ExpiresUtc.HasValue) flags |= ContainerConstants.HeaderFlags.HasExpiry;
        if (request.MaxRuns.HasValue) flags |= ContainerConstants.HeaderFlags.HasMaxRuns;
        if (signer is not null) flags |= ContainerConstants.HeaderFlags.HasVendorSignature;

        var header = new ContainerHeader
        {
            PackageId = request.PackageId,
            Flags = flags,
            KdfSalt = salt,
            KdfIterations = request.KdfIterations,
            PasswordVerifier = verifier,
            ExpiresUtc = request.ExpiresUtc,
            MaxRuns = request.MaxRuns,
            // Tied to the signer (not to RequireActivationCode) so a package that carries a header
            // signature always has the matching public key to verify it against — see VerifyHeaderIntegrity.
            VendorPublicKey = signer?.PublicKey ?? Array.Empty<byte>(),
            HeaderSignature = signer is not null ? new byte[64] : Array.Empty<byte>(), // placeholder, fixed size
            Branding = request.Branding,
            Entries = entries,
        };

        // Pass 1: length is already final because every variable-size field
        // above (offsets, signature) was fixed to its true byte width.
        int headerLength = header.ToBytes().Length;

        long cursor = headerLength;
        foreach (var entry in entries)
        {
            entry.DataOffset = cursor;
            cursor += entry.DataLength;
        }
        long blobLength = cursor;

        if (signer is not null)
            header.HeaderSignature = signer.Sign(header.ToSignablePrefixBytes());

        byte[] finalHeaderBytes = header.ToBytes();
        if (finalHeaderBytes.Length != headerLength)
            throw new InvalidOperationException("Internal error: container header length was not stable across passes.");

        await output.WriteAsync(finalHeaderBytes, ct);

        double totalPlaintext = entries.Sum(e => (double)e.PlaintextLength);
        double doneSoFar = 0;

        foreach (var video in request.Videos)
        {
            var meta = entries.First(e => e.Id == video.Id);
            byte[] contentKey = contentKeys[video.Id];

            await using var source = new FileStream(video.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 1 << 20, useAsync: true);

            byte[] plainBuf = new byte[meta.ChunkSize];
            byte[] cipherBuf = new byte[meta.ChunkSize];
            byte[] tagBuf = new byte[AesGcmCipher.TagSizeBytes];

            uint chunkIndex = 0;
            int read;
            while ((read = await ReadFullyAsync(source, plainBuf, ct)) > 0)
            {
                AesGcmCipher.EncryptChunk(contentKey, meta.ChunkNoncePrefix, chunkIndex,
                    plainBuf.AsSpan(0, read), cipherBuf.AsSpan(0, read), tagBuf);

                await output.WriteAsync(cipherBuf.AsMemory(0, read), ct);
                await output.WriteAsync(tagBuf, ct);

                chunkIndex++;
                doneSoFar += read;
                progress?.Report(totalPlaintext > 0 ? doneSoFar / totalPlaintext : 1.0);

                if (read < plainBuf.Length) break; // last (short) chunk
            }
        }

        var footer = new byte[ContainerConstants.TrailerFooterSize];
        BitConverter.GetBytes(blobLength).CopyTo(footer, 0);
        ContainerConstants.TrailerMagic.CopyTo(footer, 8);
        await output.WriteAsync(footer, ct);
    }

    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }
}
