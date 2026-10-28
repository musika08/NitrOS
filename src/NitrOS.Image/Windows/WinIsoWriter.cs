namespace NitrOS.Image.Windows;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using NitrOS.Image.Iso;
using NitrOS.Image.Systems;
using NitrOS.Live.Windows;

public sealed class WinIsoWriter : IIsoWriter
{
    private readonly string _oscdimgPath;

    public WinIsoWriter(string oscdimgPath)
    {
        _oscdimgPath = oscdimgPath ?? throw new ArgumentNullException(nameof(oscdimgPath));
    }

    public void Write(string mediaRoot, string bootOrderFile, string label, string outputIso, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var args = OscdimgArgs.For(mediaRoot, bootOrderFile, label, outputIso);
        var (exitCode, stdOut, stdErr) = ProcessRunner.Run(_oscdimgPath, args, TimeSpan.FromMinutes(90));

        if (exitCode != 0)
        {
            var lines = $"{stdOut}\n{stdErr}"
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .TakeLast(20);

            throw new InvalidOperationException($"oscdimg failed with exit code {exitCode}:\n{string.Join('\n', lines)}");
        }
    }
}
