using NitrOS.App.Logic.Ports;
using NitrOS.Live;

namespace NitrOS.App.Logic.Running;

public sealed class LiveTuneLockProbe : ITuneLockProbe
{
    public bool IsRunInProgress() => TuneEngine.IsRunInProgress();
}
