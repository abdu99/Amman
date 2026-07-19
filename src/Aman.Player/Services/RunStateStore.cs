using System.IO;
using System.Security.Cryptography;

namespace Aman.Player.Services;

/// <summary>
/// Best-effort local run counter. It is DPAPI-protected so it cannot be
/// hand-edited with a text editor, but it lives entirely on the end
/// user's machine with no server to check against — deleting
/// %LocalAppData%\Aman\state resets it. Treat MaxRuns as a soft,
/// casual-sharing deterrent, not an unbypassable limit.
/// </summary>
public static class RunStateStore
{
    private static string DirFor(Guid packageId) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aman", "state", packageId.ToString("N"));

    private static string FilePath(Guid packageId) => Path.Combine(DirFor(packageId), "runs.dat");

    public static int GetRunCount(Guid packageId)
    {
        string path = FilePath(packageId);
        if (!File.Exists(path)) return 0;

        try
        {
            byte[] protectedBytes = File.ReadAllBytes(path);
            byte[] raw = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return BitConverter.ToInt32(raw, 0);
        }
        catch
        {
            return 0; // corrupt/foreign file — fail open rather than locking out a legitimate user
        }
    }

    public static void RecordRun(Guid packageId)
    {
        int count = GetRunCount(packageId) + 1;
        Directory.CreateDirectory(DirFor(packageId));

        byte[] raw = BitConverter.GetBytes(count);
        byte[] protectedBytes = ProtectedData.Protect(raw, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath(packageId), protectedBytes);
    }
}
