namespace NitrOS.Image.Iso;

using System;
using System.Collections.Generic;
using System.IO;

public static class OscdimgArgs
{
    public static IReadOnlyList<string> For(string mediaRoot, string bootOrderFile, string label, string outputIso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(bootOrderFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputIso);

        if (bootOrderFile.Contains(' ') || mediaRoot.Contains(' '))
        {
            throw new ArgumentException("Oscdimg boot paths cannot contain spaces.");
        }

        var root = mediaRoot.TrimEnd('\\');
        var etfsboot = Path.Combine(root, "boot", "etfsboot.com");
        var efisys = Path.Combine(root, "efi", "microsoft", "boot", "efisys.bin");

        var hasBios = File.Exists(etfsboot);
        string bootData;
        if (hasBios)
        {
            bootData = $"-bootdata:2#p0,e,b{etfsboot}#pEF,e,b{efisys}";
        }
        else
        {
            bootData = $"-bootdata:1#pEF,e,b{efisys}";
        }

        return
        [
            "-m",
            "-o",
            "-u2",
            "-udfver102",
            "-h",
            $"-l{label}",
            $"-yo{bootOrderFile}",
            bootData,
            root,
            outputIso
        ];
    }
}
