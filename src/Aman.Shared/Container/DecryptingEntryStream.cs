using System.IO;
using Aman.Shared.Crypto;

namespace Aman.Shared.Container;

/// <summary>
/// A seekable, read-only view of one decrypted video's plaintext bytes.
/// Nothing is ever written to disk: each read decrypts only the AES-GCM
/// chunk(s) it actually needs, straight from the container file into
/// memory. This is what feeds the loopback streaming server that
/// <see cref="System.Windows.Controls.MediaElement"/> plays from.
/// </summary>
public sealed class DecryptingEntryStream : Stream
{
    private readonly FileStream _file;
    private readonly long _blobStart;
    private readonly VideoEntryMeta _entry;
    private readonly byte[] _contentKey;

    private long _position;
    private uint _cachedChunkIndex = uint.MaxValue;
    private byte[]? _cachedChunkPlain;

    public DecryptingEntryStream(string containerFilePath, long blobStart, VideoEntryMeta entry, byte[] contentKey)
    {
        _file = new FileStream(containerFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        _blobStart = blobStart;
        _entry = entry;
        _contentKey = contentKey;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _entry.PlaintextLength;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _entry.PlaintextLength) return 0;

        long remaining = _entry.PlaintextLength - _position;
        int toRead = (int)Math.Min(count, remaining);
        int totalRead = 0;

        while (toRead > 0)
        {
            uint chunkIndex = (uint)(_position / _entry.ChunkSize);
            int chunkOffset = (int)(_position % _entry.ChunkSize);

            byte[] plainChunk = GetOrDecryptChunk(chunkIndex);
            int available = plainChunk.Length - chunkOffset;
            int n = Math.Min(available, toRead);

            Buffer.BlockCopy(plainChunk, chunkOffset, buffer, offset + totalRead, n);

            _position += n;
            totalRead += n;
            toRead -= n;
        }

        return totalRead;
    }

    private byte[] GetOrDecryptChunk(uint chunkIndex)
    {
        if (chunkIndex == _cachedChunkIndex && _cachedChunkPlain is not null)
            return _cachedChunkPlain;

        long plainChunkStart = (long)chunkIndex * _entry.ChunkSize;
        int plainLen = (int)Math.Min(_entry.ChunkSize, _entry.PlaintextLength - plainChunkStart);

        long fileOffset = _blobStart + _entry.DataOffset + (long)chunkIndex * (_entry.ChunkSize + AesGcmCipher.TagSizeBytes);

        byte[] cipher = new byte[plainLen];
        byte[] tag = new byte[AesGcmCipher.TagSizeBytes];

        _file.Seek(fileOffset, SeekOrigin.Begin);
        _file.ReadExactly(cipher);
        _file.ReadExactly(tag);

        byte[] plain = new byte[plainLen];
        AesGcmCipher.DecryptChunk(_contentKey, _entry.ChunkNoncePrefix, chunkIndex, cipher, tag, plain);

        _cachedChunkIndex = chunkIndex;
        _cachedChunkPlain = plain;
        return plain;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _entry.PlaintextLength + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        return _position;
    }

    public override void Flush() { }
    public override int Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _file.Dispose();
        base.Dispose(disposing);
    }
}
