using NitrOS.App.Logic.Ports;

namespace NitrOS.App.Tests.Fakes;

public sealed class FakeLockProbe : ITuneLockProbe
{
    public bool IsInProgress { get; set; }

    public bool IsRunInProgress() => IsInProgress;
}
