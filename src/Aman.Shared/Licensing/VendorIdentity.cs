using System.Security.Cryptography;

namespace Aman.Shared.Licensing;

/// <summary>
/// The seller's signing identity (ECDSA P-256). The private key never
/// leaves the Encoder machine; the public key travels inside every
/// exported package so the Player can verify activation codes offline.
/// Asymmetric signing is used (instead of a shared HMAC secret) so that
/// the verification key baked into every distributed Player.exe is
/// useless for forging new activation codes even if extracted.
/// </summary>
public sealed class VendorIdentity
{
    public byte[] PublicKey { get; }
    private readonly ECDsa _ecdsa;

    private VendorIdentity(ECDsa ecdsa)
    {
        _ecdsa = ecdsa;
        PublicKey = ecdsa.ExportSubjectPublicKeyInfo();
    }

    public static VendorIdentity Create() => new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

    public static VendorIdentity LoadFromPkcs8(byte[] privateKeyPkcs8)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
        return new VendorIdentity(ecdsa);
    }

    public byte[] ExportPrivateKeyPkcs8() => _ecdsa.ExportPkcs8PrivateKey();

    /// <summary>Fixed-size 64-byte (r||s) signature — deliberately not DER, so downstream layouts can reserve a constant size.</summary>
    public byte[] Sign(byte[] data) => _ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
}
