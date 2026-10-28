using NitrOS.App.Logic.Ports;

namespace NitrOS.App.Logic.Running;

public sealed class UnprovenXboxRestorer : IXboxRestorer
{
    public bool IsAvailable => false;
    public string? UnavailableReason => "The way to add Xbox back is still being tested.";
}
