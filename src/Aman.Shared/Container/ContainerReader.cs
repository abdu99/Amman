using System.IO;
using System.Security.Cryptography;
using System.Text;
using Aman.Shared.Crypto;
using Aman.Shared.Licensing;

namespace Aman.Shared.Container;

public sealed class ContainerReader
{
    public string FilePath { get; }
    public long BlobStart { get; }
    public ContainerHeader Header { get; }

    private ContainerReader(string filePath, long blobStart, ContainerHeader header)
    {
        FilePath = filePath;
        BlobStart = blobStart;
        Header = header;
    }

    public static ContainerReader Open(string filePath)
    {
        var (blobStart, _) = SelfExeLocator.Locate(filePath);

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(blobStart, SeekOrigin.Begin);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
        var header = ContainerHeader.ReadFrom(br);

        return new ContainerReader(filePath, blobStart, header);
    }

    /// <summary>Throws if a vendor signature is present but does not verify — catches a tampered/corrupted header.</summary>
    public void VerifyHeaderIntegrity()
    {
        if ((Header.Flags & ContainerConstants.HeaderFlags.HasVendorSignature) == 0)
            return;

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Header.VendorPublicKey, out _);

        bool ok = ecdsa.VerifyData(Header.ToSignablePrefixBytes(), Header.HeaderSignature,
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        if (!ok)
            throw new InvalidDataException("AMAN package header failed signature verification — the file may have been tampered with.");
    }

    // PBKDF2 at 310k iterations costs real time (by design); cache the derived KEK per password
    // instead of repeating it once per playlist entry when unwrapping every video's content key.
    private string? _cachedKekPassword;
    private byte[]? _cachedKek;

    public bool TryVerifyPassword(string password)
        => KeyDerivation.VerifyPassword(password, Header.KdfSalt, Header.KdfIterations, Header.PasswordVerifier);

    public byte[] UnwrapContentKey(VideoEntryMeta entry, string password)
    {
        if (_cachedKek is null || _cachedKekPassword != password)
        {
            _cachedKek = KeyDerivation.DeriveKek(password, Header.KdfSalt, Header.KdfIterations);
            _cachedKekPassword = password;
        }
        return AesGcmCipher.DecryptSmall(_cachedKek, entry.EncryptedContentKeyBlob);
    }

    public Stream OpenEntryStream(VideoEntryMeta entry, byte[] contentKey)
        => new DecryptingEntryStream(FilePath, BlobStart, entry, contentKey);
}
