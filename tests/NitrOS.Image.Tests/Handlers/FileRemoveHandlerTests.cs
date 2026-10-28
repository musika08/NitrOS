namespace NitrOS.Image.Tests.Handlers;

using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Handlers;
using NitrOS.Image.Tests.Fakes;

public sealed class FileRemoveHandlerTests
{
    [Fact]
    public void FileRemove_Applies()
    {
        var fakes = new FakeImageSystems();
        const string mount = "M:\\mount";
        fakes.Files.Files["M:\\mount\\Windows\\test.txt"] = "dummy";

        var ctx = new ImageContext(fakes.Build(), mount, 1, new FirstBootPlan(), "sw1");
        var handler = ImageHandlers.For(new FileRemove("%SystemRoot%\\test.txt"));
        var result = handler.Apply(new FileRemove("%SystemRoot%\\test.txt"), ctx);

        Assert.Equal(ImageOutcome.Applied, result.Outcome);
        Assert.False(fakes.Files.Exists("M:\\mount\\Windows\\test.txt"));
    }

    [Fact]
    public void FileRemove_MissingTargetIsSkipped()
    {
        var fakes = new FakeImageSystems();
        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1");
        var handler = ImageHandlers.For(new FileRemove("%SystemRoot%\\nonexistent.txt"));
        var result = handler.Apply(new FileRemove("%SystemRoot%\\nonexistent.txt"), ctx);

        Assert.Equal(ImageOutcome.Skipped, result.Outcome);
    }

    [Fact]
    public void FileRemove_RefusesPathOutsideMount()
    {
        var fakes = new FakeImageSystems();
        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1");
        var handler = ImageHandlers.For(new FileRemove("%SystemRoot%\\..\\..\\outside.txt"));
        var result = handler.Apply(new FileRemove("%SystemRoot%\\..\\..\\outside.txt"), ctx);

        Assert.Equal(ImageOutcome.Failed, result.Outcome);
    }
}
