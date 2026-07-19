using System.Management;
using System.Security.Cryptography;
using System.Text;
using Aman.Shared.Licensing;

namespace Aman.Shared.Hardware;

/// <summary>
/// Derives a stable "Machine ID" for the current PC from a handful of
/// low-volatility WMI identifiers (CPU id, motherboard serial, boot disk
/// serial). None of these are perfectly immutable (a user can spoof or
/// change them), so this is a deterrence mechanism, not a cryptographic
/// hardware root of trust.
/// </summary>
public static class HardwareIdProvider
{
    public static string GetMachineId()
    {
        string raw = string.Join('|',
            SafeQuery("Win32_Processor", "ProcessorId"),
            SafeQuery("Win32_BaseBoard", "SerialNumber"),
            SafeQuery("Win32_DiskDrive", "SerialNumber"),
            SafeQuery("Win32_OperatingSystem", "SerialNumber"));

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        // Keep only 10 bytes (80 bits) — plenty of entropy for a per-machine
        // id, short enough to print/type as a friendly code.
        return Base32.Encode(hash.AsSpan(0, 10));
    }

    private static string SafeQuery(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (ManagementObject obj in searcher.Get())
            {
                object? value = obj[property];
                if (value is not null)
                    return value.ToString() ?? string.Empty;
            }
        }
        catch
        {
            // WMI class unavailable (locked-down VM, permissions, etc.) —
            // fall through and contribute an empty (but stable) segment.
        }
        return string.Empty;
    }
}
