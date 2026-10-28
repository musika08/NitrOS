namespace NitrOS.Image.Tests.Iso;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NitrOS.Image.Iso;

public sealed class OscdimgTests
{
    private sealed class FakeHttpGet : IHttpGet
    {
        public Func<Uri, long, (int Status, string? Location, byte[]? Body)> Handler { get; set; } =
            (u, m) => (200, null, []);

        public (int Status, string? Location, byte[]? Body) Get(Uri uri, long maxBytes, CancellationToken ct) =>
            Handler(uri, maxBytes);
    }

    [Fact]
    public void Download_FollowsMsdlToBlob()
    {
        var validBytes = Encoding.UTF8.GetBytes("fake-oscdimg-bytes");
        var http = new FakeHttpGet
        {
            Handler = (uri, max) =>
            {
                if (uri.Host == "msdl.microsoft.com")
                {
                    return (302, "https://subdomain.blob.core.windows.net/payload.bin", null);
                }
                if (uri.Host.EndsWith(".blob.core.windows.net"))
                {
                    return (200, null, validBytes);
                }
                return (404, null, null);
            }
        };

        var downloader = new OscdimgDownloader(http);
        var bytes = downloader.Download(CancellationToken.None);
        Assert.Equal(validBytes, bytes);
    }

    [Fact]
    public void Download_RefusesRedirectToOtherHost()
    {
        var http = new FakeHttpGet
        {
            Handler = (uri, max) => (302, "https://evil.com/payload.exe", null)
        };

        var downloader = new OscdimgDownloader(http);
        Assert.Throws<OscdimgRefusedException>(() => downloader.Download(CancellationToken.None));
    }

    [Fact]
    public void Download_RefusesHttpOrPort()
    {
        var http = new FakeHttpGet
        {
            Handler = (uri, max) => (302, "http://subdomain.blob.core.windows.net/payload.bin", null)
        };

        var downloader = new OscdimgDownloader(http);
        Assert.Throws<OscdimgRefusedException>(() => downloader.Download(CancellationToken.None));
    }

    [Fact]
    public void Download_RefusesSixthRedirect()
    {
        var count = 0;
        var http = new FakeHttpGet
        {
            Handler = (uri, max) =>
            {
                count++;
                return (302, $"https://blob{count}.blob.core.windows.net/test", null);
            }
        };

        var downloader = new OscdimgDownloader(http);
        Assert.Throws<OscdimgRefusedException>(() => downloader.Download(CancellationToken.None));
    }

    [Fact]
    public void Download_RefusesOversizeBody()
    {
        var large = new byte[3 * 1024 * 1024];
        var http = new FakeHttpGet
        {
            Handler = (uri, max) => (200, null, large)
        };

        var downloader = new OscdimgDownloader(http);
        Assert.Throws<OscdimgRefusedException>(() => downloader.Download(CancellationToken.None));
    }

    [Fact]
    public void Oscdimg_RefusesWrongHash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var wrongBytes = Encoding.UTF8.GetBytes("tampered bytes");
            var http = new FakeHttpGet { Handler = (u, m) => (200, null, wrongBytes) };
            var downloader = new OscdimgDownloader(http);
            var oscdimg = new Oscdimg(tempDir, downloader);

            Assert.Throws<OscdimgRefusedException>(() => oscdimg.EnsureVerified(CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(tempDir, "oscdimg.exe")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Oscdimg_ChecksExistingFileEveryTime()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var target = Path.Combine(tempDir, "oscdimg.exe");
            File.WriteAllBytes(target, [1, 2, 3]);

            var http = new FakeHttpGet();
            var downloader = new OscdimgDownloader(http);
            var oscdimg = new Oscdimg(tempDir, downloader);

            Assert.Throws<OscdimgRefusedException>(() => oscdimg.EnsureVerified(CancellationToken.None));
            // Tampered existing file must be deleted
            Assert.False(File.Exists(target));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Pin_MatchesResearchNote()
    {
        var researchNote = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../docs/research/oscdimg-source.md");
        if (File.Exists(researchNote))
        {
            var text = File.ReadAllText(researchNote);
            Assert.Contains(OscdimgPin.Sha256, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
