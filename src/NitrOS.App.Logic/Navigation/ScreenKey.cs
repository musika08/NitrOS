namespace NitrOS.App.Logic.Navigation;

public readonly record struct ScreenKey(string Name)
{
    public override string ToString() => Name;
}

public static class ScreenKeys
{
    public static readonly ScreenKey Start = new("start");
    public static readonly ScreenKey Cards = new("cards");
    public static readonly ScreenKey Switches = new("switches");
    public static readonly ScreenKey Review = new("review");
    public static readonly ScreenKey Working = new("working");
    public static readonly ScreenKey Done = new("done");
    public static readonly ScreenKey Undo = new("undo");
    public static readonly ScreenKey Drift = new("drift");
    public static readonly ScreenKey Health = new("health");
    public static readonly ScreenKey Myths = new("myths");
    public static readonly ScreenKey Settings = new("settings");
    public static readonly ScreenKey Blocked = new("blocked");
    public static readonly ScreenKey DeviceUpdates = new("device-updates");
}
