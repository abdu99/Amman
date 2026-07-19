using System.Security.Cryptography;

namespace Aman.Shared.Licensing;

/// <summary>
/// Wire format for an activation code: signature(64, raw r||s for P-256)
/// followed by the token's signable bytes, Base32-encoded and grouped for
/// easy copy/paste. Verification only needs the vendor's public key
/// (embedded in the package), never the private key.
/// </summary>
public static class ActivationCodeCodec
{
    public static string Encode(LicenseToken token, VendorIdentity signer)
    {
        byte[] payload = token.ToSignableBytes();
        byte[] signature = signer.Sign(payload);

        byte[] combined = new byte[signature.Length + payload.Length];
        signature.CopyTo(combined, 0);
        payload.CopyTo(combined, signature.Length);

        return Base32.Group(Base32.Encode(combined), groupSize: 5);
    }

    /// <summary>Returns the token if the signature verifies against <paramref name="vendorPublicKeySpki"/>; otherwise null.</summary>
    public static LicenseToken? TryDecodeAndVerify(string activationCode, byte[] vendorPublicKeySpki)
    {
        byte[] combined;
        try
        {
            combined = Base32.Decode(activationCode);
        }
        catch (FormatException)
        {
            return null;
        }

        const int signatureLength = 64; // ECDSA P-256 raw (r||s), 32 bytes each
        if (combined.Length <= signatureLength)
            return null;

        byte[] signature = combined[..signatureLength];
        byte[] payload = combined[signatureLength..];

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(vendorPublicKeySpki, out _);

        if (!ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363))
            return null;

        try
        {
            return LicenseToken.FromSignableBytes(payload);
        }
        catch
        {
            return null;
        }
    }
}
