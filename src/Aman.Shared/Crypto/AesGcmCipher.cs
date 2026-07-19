using System.Security.Cryptography;

namespace Aman.Shared.Crypto;

/// <summary>
/// AES-256-GCM primitives used throughout AMAN. Two shapes are exposed:
///  - "Small" values (content keys, metadata blobs) use a random 12-byte nonce
///    prefixed to the output, since they are encrypted once.
///  - "Chunk" values (video payload) use a deterministic nonce built from a
///    per-file random prefix + chunk index, so that any chunk can be decrypted
///    independently (required for seeking) without ever reusing a nonce for a
///    given key.
/// </summary>
public static class AesGcmCipher
{
    public const int KeySizeBytes = 32;   // AES-256
    public const int TagSizeBytes = 16;
    public const int NonceSizeBytes = 12;
    public const int ChunkNoncePrefixBytes = 8;

    public static byte[] GenerateKey()
    {
        byte[] key = new byte[KeySizeBytes];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    public static byte[] GenerateChunkNoncePrefix()
    {
        byte[] prefix = new byte[ChunkNoncePrefixBytes];
        RandomNumberGenerator.Fill(prefix);
        return prefix;
    }

    /// <summary>Encrypts a small buffer once. Output = nonce(12) || ciphertext || tag(16).</summary>
    public static byte[] EncryptSmall(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default)
    {
        byte[] nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using var gcm = new AesGcm(key, TagSizeBytes);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        byte[] output = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        nonce.CopyTo(output, 0);
        ciphertext.CopyTo(output, NonceSizeBytes);
        tag.CopyTo(output, NonceSizeBytes + ciphertext.Length);
        return output;
    }

    public static byte[] DecryptSmall(ReadOnlySpan<byte> key, ReadOnlySpan<byte> blob, ReadOnlySpan<byte> associatedData = default)
    {
        if (blob.Length < NonceSizeBytes + TagSizeBytes)
            throw new CryptographicException("Ciphertext blob too short.");

        ReadOnlySpan<byte> nonce = blob[..NonceSizeBytes];
        ReadOnlySpan<byte> tag = blob[^TagSizeBytes..];
        ReadOnlySpan<byte> ciphertext = blob[NonceSizeBytes..^TagSizeBytes];

        byte[] plaintext = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key, TagSizeBytes);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }

    /// <summary>Builds the 12-byte deterministic nonce for a given chunk: prefix(8) || chunkIndex(4, big-endian).</summary>
    public static byte[] BuildChunkNonce(ReadOnlySpan<byte> noncePrefix, uint chunkIndex)
    {
        if (noncePrefix.Length != ChunkNoncePrefixBytes)
            throw new ArgumentException($"Nonce prefix must be {ChunkNoncePrefixBytes} bytes.", nameof(noncePrefix));

        byte[] nonce = new byte[NonceSizeBytes];
        noncePrefix.CopyTo(nonce);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(ChunkNoncePrefixBytes), chunkIndex);
        return nonce;
    }

    public static void EncryptChunk(ReadOnlySpan<byte> key, ReadOnlySpan<byte> noncePrefix, uint chunkIndex,
        ReadOnlySpan<byte> plaintext, Span<byte> ciphertext, Span<byte> tag)
    {
        byte[] nonce = BuildChunkNonce(noncePrefix, chunkIndex);
        using var gcm = new AesGcm(key, TagSizeBytes);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag);
    }

    public static void DecryptChunk(ReadOnlySpan<byte> key, ReadOnlySpan<byte> noncePrefix, uint chunkIndex,
        ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag, Span<byte> plaintext)
    {
        byte[] nonce = BuildChunkNonce(noncePrefix, chunkIndex);
        using var gcm = new AesGcm(key, TagSizeBytes);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext);
    }
}
