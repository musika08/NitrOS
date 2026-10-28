namespace NitrOS.App.Logic.Ports;

using NitrOS.Core.Model;

public interface ISwitchProbe
{
    bool IsInEffect(SwitchDef sw);
    IReadOnlySet<string> GetActiveSwitchIds();
}
