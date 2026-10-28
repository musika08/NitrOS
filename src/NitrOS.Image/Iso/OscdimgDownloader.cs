namespace NitrOS.Image.Iso;

using System;
using System.IO;
using System.Net.Http;
using System.Threading;

public interface IHttpGet
{
    (int Status, string? Location, byte[]? Body) Get(Uri uri, long maxBytes, CancellationToken ct);
}

public sealed class HttpGet : IHttpGet
{
    public (int Status, string? Location, byte[]? Body) Get(Uri uri, long maxBytes, CancellationToken ct)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler);
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        using var resp = client.Send(req, HttpCompletionOption.ResponseHeadersRead, ct);

        var status = (int)resp.StatusCode;
        var location = resp.Headers.Location?.ToString();

        if (status is >= 300 and < 400)
        {
            return (status, location, null);
        }

        using var stream = resp.Content.ReadAsStream(ct);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new OscdimgRefusedException("Download exceeded maximum allowed size.");
            }
            ms.Write(buffer, 0, read);
        }

        return (status, location, ms.ToArray());
    }
}

public sealed class OscdimgRefusedException(string message) : Exception(message);

public sealed class OscdimgDownloader(IHttpGet http)
{
    private readonly IHttpGet _http = http ?? throw new ArgumentNullException(nameof(http));

    public byte[] Download(CancellationToken ct)
    {
        var currentUri = new Uri(OscdimgPin.Url);
        var redirects = 0;

        while (true)
        {
            ValidateHop(currentUri, redirects);

            var (status, location, body) = _http.Get(currentUri, OscdimgPin.MaxBytes, ct);

            if (status is >= 300 and < 400)
            {
                if (string.IsNullOrWhiteSpace(location))
                {
                    throw new OscdimgRefusedException("Redirect missing Location header.");
                }

                redirects++;
                if (redirects > OscdimgPin.MaxRedirects)
                {
                    throw new OscdimgRefusedException($"Too many redirects (exceeded limit of {OscdimgPin.MaxRedirects}).");
                }

                if (!Uri.TryCreate(currentUri, location, out var nextUri))
                {
                    throw new OscdimgRefusedException("Invalid redirect URI.");
                }

                currentUri = nextUri;
                continue;
            }

            if (status == 200)
            {
                if (body == null || body.Length == 0)
                {
                    throw new OscdimgRefusedException("Empty response body.");
                }
                if (body.Length > OscdimgPin.MaxBytes)
                {
                    throw new OscdimgRefusedException("Downloaded file exceeds maximum allowed size.");
                }
                return body;
            }

            throw new OscdimgRefusedException($"HTTP request failed with status code {status}.");
        }
    }

    private static void ValidateHop(Uri uri, int hopIndex)
    {
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new OscdimgRefusedException("Insecure protocol: only HTTPS is permitted.");
        }

        if (uri.Port != 443)
        {
            throw new OscdimgRefusedException("Non-standard port is not permitted.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new OscdimgRefusedException("URI user info is not permitted.");
        }

        if (hopIndex == 0)
        {
            if (!string.Equals(uri.Host, OscdimgPin.FirstHost, StringComparison.OrdinalIgnoreCase))
            {
                throw new OscdimgRefusedException($"Initial host must be {OscdimgPin.FirstHost}.");
            }
        }
        else
        {
            var isFirstHost = string.Equals(uri.Host, OscdimgPin.FirstHost, StringComparison.OrdinalIgnoreCase);
            var isBlob = uri.Host.EndsWith(OscdimgPin.RedirectSuffix, StringComparison.OrdinalIgnoreCase);
            if (!isFirstHost && !isBlob)
            {
                throw new OscdimgRefusedException($"Redirect host '{uri.Host}' is not permitted.");
            }
        }
    }
}
