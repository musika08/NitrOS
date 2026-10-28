namespace NitrOS.Updates;

public static class UpdateTexts
{
    public const string ConnectCharger = "Connect the charger first.";
    public const string RevocationFailed = "Could not confirm the maker's signature is still valid. Try again when online.";
    public const string BusyInstalling = "An update is installing. NitrOS cannot close until it finishes.";
    public const string NoMakerListFormat = "NitrOS has no update list for {0}. Look on your maker's website.";
    public const string ExcludeDriversPolicyNote = "Windows Update driver downloads are turned off by policy (ExcludeWUDriversInQualityUpdate).";
    public const string FirmwareWarning = "Do not turn off your PC while firmware is updating.";
    public const string WindowsUpdateUnavailable = "Windows Update did not answer. Try Check again later.";
    public const string AsusNoModelCode = "NitrOS could not read this PC's ASUS model code.";
    public const string AsusUnavailableFormat = "ASUS's update list did not answer ({0}).";
    public const string AsusShapeChanged = "ASUS's update list has changed, so NitrOS cannot read it.";
    public const string AsusNoUsableFilesFormat = "ASUS's update list has nothing for model {0}.";
}
