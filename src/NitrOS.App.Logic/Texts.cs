namespace NitrOS.App.Logic;

public static class Texts
{
    public const string AppTitle = "NitrOS";
    public const string DeviceUpdatesLater = "Device updates comes in a later NitrOS update.";
    public const string IsoLater = "Building a slim ISO comes in a later NitrOS update. You can look through the cards and switches now; nothing is built.";
    public const string NotElevated = "NitrOS needs administrator rights.";
    public const string MayBeChangedHeading = "These may still have changed something on this PC. You can undo them from Undo changes:";
    public const string IsoChoosePaths = "Choose your Windows 11 ISO and where to save the new one.";
    public const string IsoStopped = "Stopped. Your build is saved. Open Build a slim ISO to resume it or throw it away.";

    internal static readonly Type CoreAnchor = typeof(NitrOS.Core.Model.Catalog);
    internal static readonly Type LiveAnchor = typeof(NitrOS.Live.TuneEngine);
}
