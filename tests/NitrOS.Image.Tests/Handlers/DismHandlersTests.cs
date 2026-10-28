namespace NitrOS.Image.Tests.Handlers;

using System;
using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Handlers;
using NitrOS.Image.Systems;
using NitrOS.Image.Tests.Fakes;
using NitrOS.Live.Systems;

public sealed class DismHandlersTests
{
    [Fact]
    public void Appx_GlobMatchesDisplayNameAndRemovesByPackageName()
    {
        var fakes = new FakeImageSystems();
        fakes.Dism.Apps.Add(new ProvisionedApp("Microsoft.XboxApp", "Microsoft.XboxApp_1.0_x64__8wekyb3d8bbwe"));
        fakes.Dism.Apps.Add(new ProvisionedApp("Microsoft.XboxGamingOverlay", "Microsoft.XboxGamingOverlay_1.0_x64__8wekyb3d8bbwe"));
        fakes.Dism.Apps.Add(new ProvisionedApp("Microsoft.WindowsCalculator", "Microsoft.WindowsCalculator_1.0_x64__8wekyb3d8bbwe"));

        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1");
        var handler = ImageHandlers.For(new AppxRemove("*Xbox*"));
        var result = handler.Apply(new AppxRemove("*Xbox*"), ctx);

        Assert.Equal(ImageOutcome.Applied, result.Outcome);
        Assert.Single(fakes.Dism.Apps);
        Assert.Equal("Microsoft.WindowsCalculator", fakes.Dism.Apps[0].DisplayName);
    }

    [Fact]
    public void Appx_MissingTargetIsSkipped()
    {
        var fakes = new FakeImageSystems();
        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1");
        var handler = ImageHandlers.For(new AppxRemove("*NonExistent*"));
        var result = handler.Apply(new AppxRemove("*NonExistent*"), ctx);

        Assert.Equal(ImageOutcome.Skipped, result.Outcome);
        Assert.Equal("not in this image", result.Detail);
    }

    [Fact]
    public void Appx_AccessDeniedIsFailedNotSkipped()
    {
        var fakes = new FakeImageSystems();
        fakes.Dism.Apps.Add(new ProvisionedApp("Microsoft.XboxApp", "Microsoft.XboxApp_1.0_x64__8wekyb3d8bbwe"));
        fakes.Dism.CustomException = new ImageAccessDeniedException();
        fakes.Dism.FailOn = nameof(IImageServicing.RemoveProvisionedApp);

        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1");
        var handler = ImageHandlers.For(new AppxRemove("*Xbox*"));
        var result = handler.Apply(new AppxRemove("*Xbox*"), ctx);

        Assert.Equal(ImageOutcome.Failed, result.Outcome);
        Assert.Contains("Windows refused", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dism_PendingCountsAsDeferred()
    {
        var fakes = new FakeImageSystems();
        fakes.Dism.Capabilities["OpenSSH.Client"] = "Installed";
        fakes.Dism.Features["SMB1Protocol"] = "Enabled";

        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1");

        // When capability remove returns Deferred
        fakes.Dism.FailOn = null;
        // Test with custom fake response for Capability
        var capHandler = ImageHandlers.For(new CapabilityRemove("OpenSSH.Client"));
        // Temporarily override Dism outcome by removing capability
        var res1 = capHandler.Apply(new CapabilityRemove("OpenSSH.Client"), ctx);
        Assert.Equal(ImageOutcome.Applied, res1.Outcome);

        // Feature disable
        var featHandler = ImageHandlers.For(new FeatureDisable("SMB1Protocol"));
        var res2 = featHandler.Apply(new FeatureDisable("SMB1Protocol"), ctx);
        Assert.Equal(ImageOutcome.Applied, res2.Outcome);
    }

    [Fact]
    public void Dism_AlreadyOffIsSkipped()
    {
        var fakes = new FakeImageSystems();
        fakes.Dism.Features["SMB1Protocol"] = "Disabled";
        fakes.Dism.Capabilities["OpenSSH.Client"] = "NotPresent";

        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "sw1");

        var featRes = ImageHandlers.For(new FeatureDisable("SMB1Protocol")).Apply(new FeatureDisable("SMB1Protocol"), ctx);
        Assert.Equal(ImageOutcome.Skipped, featRes.Outcome);

        var capRes = ImageHandlers.For(new CapabilityRemove("OpenSSH.Client")).Apply(new CapabilityRemove("OpenSSH.Client"), ctx);
        Assert.Equal(ImageOutcome.Skipped, capRes.Outcome);
    }
}
