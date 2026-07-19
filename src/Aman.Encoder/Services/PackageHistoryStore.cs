using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Aman.Encoder.Models;

namespace Aman.Encoder.Services;

/// <summary>
/// Persists <see cref="PackageHistoryEntry"/> rows to a local JSON file.
/// Nothing here is secret (a Package ID alone is useless without the vendor
/// private key), so it's plain JSON — no DPAPI, unlike VendorIdentityStore.
/// </summary>
public static class PackageHistoryStore
{
    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aman", "package-history.json");

    public static List<PackageHistoryEntry> Load()
    {
        if (!File.Exists(FilePath)) return new List<PackageHistoryEntry>();

        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<PackageHistoryEntry>>(json) ?? new List<PackageHistoryEntry>();
        }
        catch
        {
            return new List<PackageHistoryEntry>(); // corrupt/foreign file — start fresh rather than crash the app
        }
    }

    public static void Append(PackageHistoryEntry entry)
    {
        var all = Load();
        all.Insert(0, entry); // most recent first

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string json = JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
