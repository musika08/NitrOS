using System.Globalization;
using NitrOS.Live;

namespace NitrOS.App.Logic.Formatting;

public static class Plain
{
    public static string Memory(long mb)
    {
        if (mb < 1024)
            return $"{mb} MB";

        return $"{(mb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} GB";
    }

    public static string MemoryLine(MemoryInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var line = $"{(info.VisibleMb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} GB available to Windows.";
        if (info.ReservedMb >= 512)
        {
            line += $" About {(info.ReservedMb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} GB of {(info.InstalledMb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} GB is set aside for graphics and hardware.";
        }
        return line;
    }

    public static string Count(int n, string one, string many) =>
        $"{n} {(n == 1 ? one : many)}";
}
