namespace NitrOS.Image.Windows;

using System;
using System.Diagnostics;
using System.IO;
using NitrOS.Image.Systems;

public sealed class WinHostFacts : IHostFacts
{
    public Version DismVersion()
    {
        var dism = Path.Combine(Environment.SystemDirectory, "dism.exe");
        if (File.Exists(dism))
        {
            var vi = FileVersionInfo.GetVersionInfo(dism);
            return new Version(vi.FileMajorPart, vi.FileMinorPart, vi.FileBuildPart);
        }
        return new Version(0, 0, 0);
    }

    public string WindowsDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
}
