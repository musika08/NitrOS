using NitrOS.Updates.Windows;
using Xunit;

namespace NitrOS.Updates.Tests.LiveNet;

public sealed class WindowsUpdateLiveTests
{
    [Fact]
    [Trait("Lane", "LiveNet")]
    public async Task WindowsUpdateLive_SearchReadsOnly()
    {
        var agent = new WinUpdateAgent();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var records = await agent.SearchDriversAsync(cts.Token);
        Assert.NotNull(records);
    }
}
