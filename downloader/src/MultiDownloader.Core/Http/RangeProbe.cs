using System.Net;
using System.Net.Http.Headers;

namespace MultiDownloader.Core.Http;

public sealed record RangeProbeResult(bool SupportsRanges, long? ContentLength, string? SuggestedFileName, string? ContentType);

/// <summary>Figures out, before downloading, whether a URL supports HTTP range requests
/// (needed for both multi-connection splitting and resuming a paused download) and how
/// large/what it's called.</summary>
public static class RangeProbe
{
    public static async Task<RangeProbeResult> ProbeAsync(HttpClient http, string url, CancellationToken ct)
    {
        bool supportsRanges = false;
        long? contentLength = null;
        string? contentType = null;

        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await http.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            if (headResponse.IsSuccessStatusCode)
            {
                supportsRanges = headResponse.Headers.AcceptRanges.Contains("bytes");
                contentLength = headResponse.Content.Headers.ContentLength;
                contentType = headResponse.Content.Headers.ContentType?.MediaType;
            }
        }
        catch
        {
            // Some servers reject HEAD entirely — fall through to the ranged-GET probe below.
        }

        if (!supportsRanges || contentLength is null)
        {
            try
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Range = new RangeHeaderValue(0, 0);
                using var getResponse = await http.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, ct);
                if (getResponse.StatusCode == HttpStatusCode.PartialContent)
                {
                    supportsRanges = true;
                    contentLength = getResponse.Content.Headers.ContentRange?.Length ?? contentLength;
                }
                contentType ??= getResponse.Content.Headers.ContentType?.MediaType;
                contentLength ??= getResponse.StatusCode == HttpStatusCode.OK
                    ? getResponse.Content.Headers.ContentLength
                    : null;
            }
            catch
            {
                // Leave defaults — caller falls back to a single, non-resumable connection.
            }
        }

        string? fileName = ExtractFileName(url);
        return new RangeProbeResult(supportsRanges, contentLength, fileName, contentType);
    }

    private static string? ExtractFileName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var name = Uri.UnescapeDataString(Path.GetFileName(uri.LocalPath));
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }
}
