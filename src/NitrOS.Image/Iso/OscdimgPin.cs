namespace NitrOS.Image.Iso;

public static class OscdimgPin
{
    public const string Url = "https://msdl.microsoft.com/download/symbols/oscdimg.exe/688CABB065000/oscdimg.exe";
    public const string Sha256 = "2000160B2C5044691B2F9A0AC308E5207F273D4880A572457AF16D05886BA861";
    public const string FirstHost = "msdl.microsoft.com";
    public const string RedirectSuffix = ".blob.core.windows.net";
    public const int MaxRedirects = 5;
    public const long MaxBytes = 2 * 1024 * 1024;
}
