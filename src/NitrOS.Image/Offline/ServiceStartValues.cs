namespace NitrOS.Image.Offline;

using System;
using NitrOS.Core.Model;

public static class ServiceStartValues
{
    public static (int Start, int? DelayedAutostart) For(ServiceStartType t) =>
        t switch
        {
            ServiceStartType.Boot => (0, null),
            ServiceStartType.System => (1, null),
            ServiceStartType.Automatic => (2, 0),
            ServiceStartType.AutomaticDelayed => (2, 1),
            ServiceStartType.Manual => (3, null),
            ServiceStartType.Disabled => (4, null),
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
        };
}
