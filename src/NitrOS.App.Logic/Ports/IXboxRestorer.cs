namespace NitrOS.App.Logic.Ports;

public interface IXboxRestorer
{
    bool IsAvailable { get; }
    string? UnavailableReason { get; }
}
