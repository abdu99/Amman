using System.IO;
using System.Security.Cryptography;
using Aman.Shared.Licensing;

namespace Aman.Encoder.Services;

/// <summary>
/// Persists the seller's ECDSA signing identity across Encoder sessions.
/// The private key is protected at rest with Windows DPAPI (CurrentUser
/// scope) — it never leaves this machine and is never written into any
/// exported package (only the public key is).
/// </summary>
public static class VendorIdentityStore
{
    private static string VaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aman", "vendor.key");

    public static VendorIdentity LoadOrCreate()
    {
        if (File.Exists(VaultPath))
        {
            byte[] protectedBytes = File.ReadAllBytes(VaultPath);
            byte[] pkcs8 = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return VendorIdentity.LoadFromPkcs8(pkcs8);
        }

        var identity = VendorIdentity.Create();
        byte[] pkcs8New = identity.ExportPrivateKeyPkcs8();
        byte[] protectedNew = ProtectedData.Protect(pkcs8New, optionalEntropy: null, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(Path.GetDirectoryName(VaultPath)!);
        File.WriteAllBytes(VaultPath, protectedNew);

        return identity;
    }
}
