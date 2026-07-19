using System;

namespace Aman.Shared.Licensing;

public enum LicenseStatus
{
    Valid,
    InvalidSignature,
    WrongPackage,
    WrongMachine,
    Expired,
    RunLimitReached,
}

public static class LicenseEvaluator
{
    /// <summary>
    /// Pure decision function — does not touch disk or the clock source
    /// directly, so it can be unit tested and reused by both apps.
    /// </summary>
    public static LicenseStatus Evaluate(LicenseToken token, Guid expectedPackageId, string currentMachineId,
        DateTime nowUtc, int runsSoFar)
    {
        if (token.PackageId != expectedPackageId)
            return LicenseStatus.WrongPackage;

        if (!string.IsNullOrEmpty(token.MachineId) &&
            !string.Equals(token.MachineId, currentMachineId, StringComparison.OrdinalIgnoreCase))
            return LicenseStatus.WrongMachine;

        if (token.ExpiresUtc.HasValue && nowUtc > token.ExpiresUtc.Value)
            return LicenseStatus.Expired;

        if (token.MaxRuns.HasValue && runsSoFar >= token.MaxRuns.Value)
            return LicenseStatus.RunLimitReached;

        return LicenseStatus.Valid;
    }
}
