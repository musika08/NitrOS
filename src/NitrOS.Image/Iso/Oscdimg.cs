namespace NitrOS.Image.Iso;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using NitrOS.Image.Building;

public sealed class Oscdimg : IOscdimg
{
    private readonly string _toolsDir;
    private readonly OscdimgDownloader _downloader;
    private readonly Func<string, bool>? _signatureCheck;

    public Oscdimg(string toolsDir, OscdimgDownloader downloader, Func<string, bool>? signatureCheck = null)
    {
        _toolsDir = toolsDir ?? throw new ArgumentNullException(nameof(toolsDir));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _signatureCheck = signatureCheck;
    }

    public string EnsureVerified(CancellationToken ct)
    {
        Directory.CreateDirectory(_toolsDir);
        var targetPath = Path.Combine(_toolsDir, "oscdimg.exe");
        var tmpPath = Path.Combine(_toolsDir, "oscdimg.exe.tmp");

        if (!File.Exists(targetPath))
        {
            var bytes = _downloader.Download(ct);
            File.WriteAllBytes(tmpPath, bytes);

            try
            {
                VerifyFile(tmpPath);
                File.Move(tmpPath, targetPath, overwrite: true);
            }
            catch
            {
                if (File.Exists(tmpPath))
                {
                    try { File.Delete(tmpPath); } catch { }
                }
                throw;
            }
        }
        else
        {
            try
            {
                VerifyFile(targetPath);
            }
            catch
            {
                try { File.Delete(targetPath); } catch { }
                throw;
            }
        }

        return targetPath;
    }

    private void VerifyFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = SHA256.HashData(bytes);
        var hashString = Convert.ToHexString(hash);

        if (!string.Equals(hashString, OscdimgPin.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new OscdimgRefusedException($"NitrOS could not get Microsoft's ISO tool safely: SHA-256 hash mismatch. Expected {OscdimgPin.Sha256}, got {hashString}.");
        }

        if (_signatureCheck != null && !_signatureCheck(path))
        {
            throw new OscdimgRefusedException("NitrOS could not get Microsoft's ISO tool safely: signature verification failed.");
        }
    }
}
