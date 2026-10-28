namespace NitrOS.Updates.Installing;

public sealed class Downloader(HttpClient http)
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<string> DownloadAsync(
        Uri url,
        SourcePolicy policy,
        long? expectedSize,
        string folder,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        if (url == null) throw new ArgumentNullException(nameof(url));
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Folder is required.", nameof(folder));

        // Global Constraints: HTTPS only, host per hop allowed, at most 5 redirects, size cap
        var currentUri = url;
        int redirectCount = 0;
        const int maxRedirects = 5;

        HttpResponseMessage response;
        while (true)
        {
            if (currentUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"Insecure download protocol rejected: {currentUri.Scheme}");
            }

            if (!policy.AllowedHosts.Any(h => string.Equals(h, currentUri.Host, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Host not permitted by source policy: {currentUri.Host}");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400 && response.Headers.Location != null)
            {
                redirectCount++;
                if (redirectCount > maxRedirects)
                {
                    throw new InvalidOperationException("Too many redirects during download.");
                }

                currentUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);

                response.Dispose();
                continue;
            }

            response.EnsureSuccessStatusCode();
            break;
        }

        Directory.CreateDirectory(folder);

        var fileName = Path.GetFileName(currentUri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "update.bin";
        }

        var partPath = Path.Combine(folder, fileName + ".part");
        var finalPath = Path.Combine(folder, fileName);

        if (File.Exists(partPath)) File.Delete(partPath);
        if (File.Exists(finalPath)) File.Delete(finalPath);

        long? totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
        long maxAllowedBytes = totalBytes.HasValue ? (long)(totalBytes.Value * 1.1) + 4096 : 2L * 1024 * 1024 * 1024; // 2GB cap fallback

        try
        {
            using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
            using (var fileStream = new FileStream(partPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    totalRead += read;
                    if (totalRead > maxAllowedBytes)
                    {
                        throw new InvalidOperationException($"Download size exceeded cap: {totalRead} bytes.");
                    }

                    await fileStream.WriteAsync(buffer, 0, read, ct);
                    progress?.Report(new InstallProgress(InstallStage.Downloading, totalRead, totalBytes, $"Downloading {fileName}..."));
                }
            }

            File.Move(partPath, finalPath, overwrite: true);
            return finalPath;
        }
        catch
        {
            try
            {
                if (File.Exists(partPath)) File.Delete(partPath);
            }
            catch { }
            throw;
        }
    }
}
