using NitrOS.Core.Selecting;

namespace NitrOS.Live.Tests;

public class SmokeTests
{
    [Fact]
    public void LiveSmoke_ReferencesCore()
    {
        Assert.Equal(TargetMode.TunePc, default(TargetMode));
    }
}
