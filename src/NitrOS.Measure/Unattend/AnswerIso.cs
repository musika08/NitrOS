using System.Runtime.InteropServices;
using System.Text;

namespace NitrOS.Measure.Unattend;

public static class AnswerIso
{
    [DllImport("shlwapi.dll")]
    private static extern System.Runtime.InteropServices.ComTypes.IStream SHCreateMemStream(byte[] pInit, uint cbInit);

    public static void Write(string isoPath, string fileName, string content)
    {
        ArgumentNullException.ThrowIfNull(isoPath);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var dir = Path.GetDirectoryName(isoPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        var inStream = SHCreateMemStream(bytes, (uint)bytes.Length);

        var imageType = Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage")
            ?? throw new PlatformNotSupportedException("IMAPI2FS.MsftFileSystemImage is not available.");

        dynamic image = Activator.CreateInstance(imageType)
            ?? throw new InvalidOperationException("Could not create IMAPI2FS.MsftFileSystemImage instance.");

        // FileSystemsToCreate: 1 = ISO 9660, 2 = Joliet -> 3 = ISO 9660 + Joliet
        image.FileSystemsToCreate = 3;
        image.Root.AddFile(fileName, inStream);

        dynamic resultImage = image.CreateResultImage();
        var comStream = (System.Runtime.InteropServices.ComTypes.IStream)resultImage.ImageStream;

        byte[] buffer = new byte[65536];
        using var fs = File.Create(isoPath);
        int bytesRead;

        IntPtr pcbRead = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            do
            {
                comStream.Read(buffer, buffer.Length, pcbRead);
                bytesRead = Marshal.ReadInt32(pcbRead);
                if (bytesRead > 0)
                {
                    fs.Write(buffer, 0, bytesRead);
                }
            } while (bytesRead > 0);
        }
        finally
        {
            Marshal.FreeHGlobal(pcbRead);
        }
    }
}
