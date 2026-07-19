namespace Aman.Shared.Container;

public sealed class VideoEntryMeta
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string OriginalFileName { get; init; }
    public required string Extension { get; init; } // e.g. ".mp4" — drives the served Content-Type
    public required long PlaintextLength { get; init; }
    public required int ChunkSize { get; init; }
    public required byte[] EncryptedContentKeyBlob { get; init; } // AesGcmCipher.EncryptSmall(kek, contentKey)
    public required byte[] ChunkNoncePrefix { get; init; } // 8 bytes

    /// <summary>Offset of this entry's encrypted chunk stream, relative to the start of the container blob.</summary>
    public long DataOffset { get; set; }

    /// <summary>Total bytes on disk for this entry's chunk stream (ciphertext + per-chunk tags).</summary>
    public long DataLength { get; set; }

    public int ChunkCount => (int)Math.Ceiling(PlaintextLength / (double)ChunkSize);

    internal void WriteTo(BinaryWriter w)
    {
        w.Write(Id.ToByteArray());
        w.Write(Title);
        w.Write(OriginalFileName);
        w.Write(Extension);
        w.Write(PlaintextLength);
        w.Write(ChunkSize);
        w.Write(EncryptedContentKeyBlob.Length);
        w.Write(EncryptedContentKeyBlob);
        w.Write(ChunkNoncePrefix);
        w.Write(DataOffset);
        w.Write(DataLength);
    }

    internal static VideoEntryMeta ReadFrom(BinaryReader r)
    {
        var id = new Guid(r.ReadBytes(16));
        string title = r.ReadString();
        string originalFileName = r.ReadString();
        string extension = r.ReadString();
        long plaintextLength = r.ReadInt64();
        int chunkSize = r.ReadInt32();
        int keyBlobLen = r.ReadInt32();
        byte[] keyBlob = r.ReadBytes(keyBlobLen);
        byte[] noncePrefix = r.ReadBytes(8);
        long dataOffset = r.ReadInt64();
        long dataLength = r.ReadInt64();

        return new VideoEntryMeta
        {
            Id = id,
            Title = title,
            OriginalFileName = originalFileName,
            Extension = extension,
            PlaintextLength = plaintextLength,
            ChunkSize = chunkSize,
            EncryptedContentKeyBlob = keyBlob,
            ChunkNoncePrefix = noncePrefix,
            DataOffset = dataOffset,
            DataLength = dataLength,
        };
    }
}
