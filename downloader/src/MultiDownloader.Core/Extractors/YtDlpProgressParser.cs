using System.Text.RegularExpressions;

namespace MultiDownloader.Core.Extractors;

public readonly record struct YtDlpProgress(
    double? Percent,
    long? TotalBytes,
    double? SpeedBytesPerSec,
    TimeSpan? Eta,
    bool IsMerging,
    string? DestinationFileName);

/// <summary>Parses yt-dlp's `--newline` progress output, e.g.:
/// "[download]  45.2% of   12.34MiB at    1.23MiB/s ETA 00:08"
/// "[download] Destination: My Video.mp4"
/// "[Merger] Merging formats into \"My Video.mp4\""</summary>
public static class YtDlpProgressParser
{
    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+(?<percent>[\d.]+)%\s+of\s+(?:~\s*)?(?<size>[\d.]+)(?<sizeunit>[KMGT]i?B)" +
        @"(?:\s+at\s+(?:(?<speed>[\d.]+)(?<speedunit>[KMGT]i?B)/s|Unknown speed))?" +
        @"(?:\s+ETA\s+(?<eta>[\d:]+|Unknown))?",
        RegexOptions.Compiled);

    private static readonly Regex DestinationRegex =
        new(@"\[download\]\s+Destination:\s+(?<name>.+)$", RegexOptions.Compiled);

    private static readonly Regex AlreadyDownloadedRegex =
        new(@"\[download\]\s+(?<name>.+?)\s+has already been downloaded", RegexOptions.Compiled);

    public static YtDlpProgress? Parse(string line)
    {
        if (line.StartsWith("[Merger]", StringComparison.Ordinal) ||
            line.StartsWith("[ExtractAudio]", StringComparison.Ordinal) ||
            line.StartsWith("[VideoConvertor]", StringComparison.Ordinal))
        {
            return new YtDlpProgress(null, null, null, null, IsMerging: true, null);
        }

        var destMatch = DestinationRegex.Match(line);
        if (destMatch.Success)
        {
            return new YtDlpProgress(null, null, null, null, false, destMatch.Groups["name"].Value.Trim());
        }

        var alreadyMatch = AlreadyDownloadedRegex.Match(line);
        if (alreadyMatch.Success)
        {
            return new YtDlpProgress(100, null, null, null, false, alreadyMatch.Groups["name"].Value.Trim());
        }

        var match = ProgressRegex.Match(line);
        if (!match.Success) return null;

        double percent = double.Parse(match.Groups["percent"].Value, System.Globalization.CultureInfo.InvariantCulture);
        long? total = ToBytes(match.Groups["size"].Value, match.Groups["sizeunit"].Value);
        double? speed = match.Groups["speed"].Success
            ? ToBytes(match.Groups["speed"].Value, match.Groups["speedunit"].Value)
            : null;
        TimeSpan? eta = ParseEta(match.Groups["eta"].Value);

        return new YtDlpProgress(percent, total, speed, eta, false, null);
    }

    private static long? ToBytes(string value, string unit)
    {
        if (string.IsNullOrEmpty(value)) return null;
        double n = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        double multiplier = unit.TrimEnd('i', 'B') switch
        {
            "K" => 1024,
            "M" => 1024d * 1024,
            "G" => 1024d * 1024 * 1024,
            "T" => 1024d * 1024 * 1024 * 1024,
            _ => 1,
        };
        return (long)(n * multiplier);
    }

    private static TimeSpan? ParseEta(string value)
    {
        if (string.IsNullOrEmpty(value) || value == "Unknown") return null;
        var parts = value.Split(':').Select(int.Parse).ToArray();
        return parts.Length switch
        {
            3 => new TimeSpan(parts[0], parts[1], parts[2]),
            2 => new TimeSpan(0, parts[0], parts[1]),
            1 => TimeSpan.FromSeconds(parts[0]),
            _ => null,
        };
    }
}
