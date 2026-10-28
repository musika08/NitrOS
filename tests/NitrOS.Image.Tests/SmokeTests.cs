namespace NitrOS.Image.Tests;

using NitrOS.Core.Model;
using NitrOS.Live.Systems;

public sealed class SmokeTests
{
    [Fact]
    public void ImageSmoke_ReferencesCoreAndLive()
    {
        var coreType = typeof(CardId);
        var liveType = typeof(LiveHive);
        var imageType = typeof(NitrOS.Image.ImageMarker);

        Assert.NotNull(coreType);
        Assert.NotNull(liveType);
        Assert.NotNull(imageType);
    }
}
