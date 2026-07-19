using System.Security.Cryptography;
using System.Text;

namespace Aman.Shared.Crypto;

/// <summary>
/// Turns a user password into (a) a Key-Encryption-Key used to unwrap the
/// per-video content keys, and (b) a verifier that lets the Player reject a
/// wrong password instantly, without that verifier being useful to recover
/// the KEK itself (it is an independent HKDF branch, not the KEK).
/// </summary>
public static class KeyDerivation
{
    public const int SaltSizeBytes = 16;
    public const int DefaultIterations = 310_000; // OWASP 2023+ guidance for PBKDF2-HMAC-SHA256

    private static readonly byte[] KekInfo = Encoding.UTF8.GetBytes("AMAN-KEK-v1");
    private static readonly byte[] VerifierInfo = Encoding.UTF8.GetBytes("AMAN-VERIFY-v1");

    public static byte[] GenerateSalt()
    {
        byte[] salt = new byte[SaltSizeBytes];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    private static byte[] Stretch(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormKC)),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);
    }

    public static byte[] DeriveKek(string password, byte[] salt, int iterations)
    {
        byte[] stretched = Stretch(password, salt, iterations);
        return HKDF.Expand(HashAlgorithmName.SHA256, stretched, 32, KekInfo);
    }

    public static byte[] DeriveVerifier(string password, byte[] salt, int iterations)
    {
        byte[] stretched = Stretch(password, salt, iterations);
        return HKDF.Expand(HashAlgorithmName.SHA256, stretched, 32, VerifierInfo);
    }

    public static bool VerifyPassword(string password, byte[] salt, int iterations, byte[] expectedVerifier)
    {
        byte[] actual = DeriveVerifier(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expectedVerifier);
    }
}
