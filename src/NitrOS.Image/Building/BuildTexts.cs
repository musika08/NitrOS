namespace NitrOS.Image.Building;

public static class BuildTexts
{
    public const string NoSourceOrOutput = "Source or output ISO not specified.";
    public const string SourceMissing = "Source ISO or folder not found.";
    public const string OutputExists = "A file with that name is already there. Pick a new name; NitrOS never writes over a file.";
    public const string OutputInsideSource = "Output file cannot be inside the source.";
    public const string OutputFolderMissing = "Folder for output ISO does not exist.";
    public const string NotNtfs = "The new ISO needs an NTFS drive for its work folder.";
    public const string LowDiskSpace = "Building needs about 30 GB free on the drive you save to.";
    public const string HostDismTooOld = "This PC's Windows image tool (DISM) is too old for Windows 11 24H2 images. Update Windows first.";
    public const string OnBattery = "Plug in the charger. Building an ISO takes a long time and a lot of disk work, so NitrOS does not start on battery.";
    public const string BatteryLow = "The battery is low, so the build stopped safely. Plug in and resume it from Build a slim ISO.";
    public const string Busy = "Another ISO build is already running.";
    public const string SourceChanged = "The source ISO changed since this build started. Throw the build away and start again.";
    public const string CatalogChanged = "NitrOS's switch list changed since this build started. Throw the build away and start again.";
}
