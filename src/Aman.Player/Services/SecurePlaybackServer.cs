using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Aman.Shared.Container;

namespace Aman.Player.Services;

/// <summary>
/// Serves decrypted video bytes to a local <c>MediaElement</c> over an
/// HTTP-Range-capable loopback server. This is what lets AMAN reuse
/// Windows' own Media Foundation pipeline (broad codec support, no
/// bundled native DLLs, genuinely one exe) while never writing a
/// decrypted frame to disk — every response is decrypted straight from
/// the container into the socket. Bound to 127.0.0.1 only, with a random
/// per-launch path token so other local processes can't casually guess
/// the URL; that is defense-in-depth, not a hard security boundary.
/// </summary>
public sealed class SecurePlaybackServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly ContainerReader _reader;
    private readonly IReadOnlyDictionary<Guid, byte[]> _contentKeys;
    private readonly string _sessionToken;

    public int Port { get; }

    public SecurePlaybackServer(ContainerReader reader, IReadOnlyDictionary<Guid, byte[]> contentKeys)
    {
        _reader = reader;
        _contentKeys = contentKeys;
        _sessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        Port = GetFreeTcpPort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
    }

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    public Uri GetEntryUri(Guid entryId) => new($"http://127.0.0.1:{Port}/{_sessionToken}/{entryId:N}");

    private async Task AcceptLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                break; // listener was stopped/disposed
            }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var request = ctx.Request;
        var response = ctx.Response;

        try
        {
            response.Headers["Cache-Control"] = "no-store";

            string[] segments = request.Url!.AbsolutePath.Trim('/').Split('/');
            if (segments.Length != 2 || segments[0] != _sessionToken || !Guid.TryParseExact(segments[1], "N", out var entryId))
            {
                response.StatusCode = 403;
                return;
            }

            var entry = _reader.Header.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry is null || !_contentKeys.TryGetValue(entryId, out byte[]? key))
            {
                response.StatusCode = 404;
                return;
            }

            using var plaintext = _reader.OpenEntryStream(entry, key);
            long total = plaintext.Length;

            long start = 0, end = total - 1;
            bool isPartial = false;
            string? rangeHeader = request.Headers["Range"];
            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                isPartial = true;
                string[] parts = rangeHeader["bytes=".Length..].Split('-');
                if (parts.Length == 2)
                {
                    if (long.TryParse(parts[0], out long s)) start = s;
                    if (parts[1].Length > 0 && long.TryParse(parts[1], out long e)) end = e;
                }
            }
            end = Math.Min(end, total - 1);
            long length = Math.Max(0, end - start + 1);

            response.StatusCode = isPartial ? 206 : 200;
            response.ContentType = MimeTypeFor(entry.Extension);
            response.Headers["Accept-Ranges"] = "bytes";
            if (isPartial) response.Headers["Content-Range"] = $"bytes {start}-{end}/{total}";
            response.ContentLength64 = length;

            plaintext.Seek(start, SeekOrigin.Begin);
            byte[] buffer = new byte[64 * 1024];
            long remaining = length;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int n = await plaintext.ReadAsync(buffer.AsMemory(0, toRead));
                if (n == 0) break;
                await response.OutputStream.WriteAsync(buffer.AsMemory(0, n));
                remaining -= n;
            }
        }
        catch (HttpListenerException)
        {
            // Client disconnected mid-stream (e.g. user seeked, aborting the in-flight request) — not actionable.
        }
        catch (IOException)
        {
            // Same as above, surfaced as a stream write failure instead.
        }
        finally
        {
            try { response.OutputStream.Close(); } catch { /* already closed by the exception path above */ }
        }
    }

    private static string MimeTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "video/mp4",
        ".mkv" => "video/x-matroska",
        ".mov" => "video/quicktime",
        ".avi" => "video/x-msvideo",
        ".wmv" => "video/x-ms-wmv",
        ".webm" => "video/webm",
        _ => "application/octet-stream",
    };

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();
    }
}
