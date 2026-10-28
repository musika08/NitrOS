namespace NitrOS.Updates.Tests;

using NitrOS.Live.Windows;

public sealed class SmokeTests
{
    [Fact]
    public void UpdatesSmoke_ReferencesLive()
    {
        Assert.True(Authenticode.NamesMatch("ASUS", "ASUSTeK COMPUTER INC."));
    }
}
