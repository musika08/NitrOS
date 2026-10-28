namespace NitrOS.Updates.Tests.Reading;

using NitrOS.Core.Model;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Updates.Reading;

public sealed class WindowsUpdatePolicyTests
{
    [Fact]
    public void WindowsUpdatePolicy_ReadsExcludeFlag()
    {
        var reg = new FakeRegistry();
        var policy = new WindowsUpdatePolicy(reg);

        Assert.False(policy.DriversExcluded());

        reg.SetValue(
            LiveHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
            "ExcludeWUDriversInQualityUpdate",
            new RegValue(RegistryKind.Dword, "1"),
            RegView.Default);

        Assert.True(policy.DriversExcluded());
    }

    [Fact]
    public void WindowsUpdatePolicy_NeverWrites()
    {
        var reg = new FakeRegistry();
        var policy = new WindowsUpdatePolicy(reg);

        _ = policy.DriversExcluded();

        Assert.Empty(reg.Writes);
    }
}
