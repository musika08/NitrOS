namespace NitrOS.Image.Handlers;

using System;
using System.Linq;
using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.Systems;
using NitrOS.Live;
using NitrOS.Live.Systems;

public sealed class AppxRemoveHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var appx = (AppxRemove)action;
        IReadOnlyList<ProvisionedApp> apps;
        try
        {
            apps = ctx.Systems.Dism.ProvisionedApps(ctx.MountDir);
        }
        catch (ImageAccessDeniedException ex)
        {
            return new ImageActionResult(ImageOutcome.Failed, ex.Message);
        }

        var matching = apps.Where(a => Glob.IsMatch(appx.Name, a.DisplayName)).ToList();
        if (matching.Count == 0)
        {
            return new ImageActionResult(ImageOutcome.Skipped, "not in this image");
        }

        foreach (var app in matching)
        {
            try
            {
                var outcome = ctx.Systems.Dism.RemoveProvisionedApp(ctx.MountDir, app.PackageName);
                if (outcome == DismOutcome.Failed)
                {
                    return new ImageActionResult(ImageOutcome.Failed, "Remove-ProvisionedAppxPackage failed");
                }
            }
            catch (ImageAccessDeniedException)
            {
                return new ImageActionResult(ImageOutcome.Failed, "Windows refused to change the built-in app list in the image");
            }
            catch (Exception ex)
            {
                return new ImageActionResult(ImageOutcome.Failed, ex.Message);
            }
        }

        return new ImageActionResult(ImageOutcome.Applied);
    }
}

public sealed class CapabilityRemoveHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var cap = (CapabilityRemove)action;
        string? state;
        try
        {
            state = ctx.Systems.Dism.CapabilityState(ctx.MountDir, cap.Name);
        }
        catch (ImageAccessDeniedException ex)
        {
            return new ImageActionResult(ImageOutcome.Failed, ex.Message);
        }

        if (state is null || string.Equals(state, "NotPresent", StringComparison.OrdinalIgnoreCase))
        {
            return new ImageActionResult(ImageOutcome.Skipped, "not in this image");
        }

        if (string.Equals(state, "Disabled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "DisabledWithPayloadRemoved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Removed", StringComparison.OrdinalIgnoreCase))
        {
            return new ImageActionResult(ImageOutcome.Skipped, "already off");
        }

        try
        {
            var outcome = ctx.Systems.Dism.RemoveCapability(ctx.MountDir, cap.Name);
            return outcome switch
            {
                DismOutcome.Deferred or DismOutcome.RebootRequired =>
                    new ImageActionResult(ImageOutcome.Deferred, "finishes when Windows first starts"),
                DismOutcome.Failed =>
                    new ImageActionResult(ImageOutcome.Failed, "Remove-Capability failed"),
                _ => new ImageActionResult(ImageOutcome.Applied)
            };
        }
        catch (ImageAccessDeniedException ex)
        {
            return new ImageActionResult(ImageOutcome.Failed, ex.Message);
        }
    }
}

public sealed class FeatureDisableHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var feat = (FeatureDisable)action;
        string? state;
        try
        {
            state = ctx.Systems.Dism.FeatureState(ctx.MountDir, feat.Name);
        }
        catch (ImageAccessDeniedException ex)
        {
            return new ImageActionResult(ImageOutcome.Failed, ex.Message);
        }

        if (state is null || string.Equals(state, "NotPresent", StringComparison.OrdinalIgnoreCase))
        {
            return new ImageActionResult(ImageOutcome.Skipped, "not in this image");
        }

        if (string.Equals(state, "Disabled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "DisabledWithPayloadRemoved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Removed", StringComparison.OrdinalIgnoreCase))
        {
            return new ImageActionResult(ImageOutcome.Skipped, "already off");
        }

        try
        {
            var outcome = ctx.Systems.Dism.DisableFeature(ctx.MountDir, feat.Name);
            return outcome switch
            {
                DismOutcome.Deferred or DismOutcome.RebootRequired =>
                    new ImageActionResult(ImageOutcome.Deferred, "finishes when Windows first starts"),
                DismOutcome.Failed =>
                    new ImageActionResult(ImageOutcome.Failed, "Disable-Feature failed"),
                _ => new ImageActionResult(ImageOutcome.Applied)
            };
        }
        catch (ImageAccessDeniedException ex)
        {
            return new ImageActionResult(ImageOutcome.Failed, ex.Message);
        }
    }
}

public sealed class PackageRemoveHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var pkg = (PackageRemove)action;
        IReadOnlyList<string> packages;
        try
        {
            packages = ctx.Systems.Dism.Packages(ctx.MountDir);
        }
        catch (ImageAccessDeniedException ex)
        {
            return new ImageActionResult(ImageOutcome.Failed, ex.Message);
        }

        var matching = packages.Where(p => Glob.IsMatch(pkg.Pattern, p)).ToList();
        if (matching.Count == 0)
        {
            return new ImageActionResult(ImageOutcome.Skipped, "not in this image");
        }

        var anyDeferred = false;
        foreach (var p in matching)
        {
            try
            {
                var outcome = ctx.Systems.Dism.RemovePackage(ctx.MountDir, p);
                if (outcome is DismOutcome.Deferred or DismOutcome.RebootRequired)
                {
                    anyDeferred = true;
                }
                else if (outcome == DismOutcome.Failed)
                {
                    return new ImageActionResult(ImageOutcome.Failed, "Remove-Package failed");
                }
            }
            catch (ImageAccessDeniedException ex)
            {
                return new ImageActionResult(ImageOutcome.Failed, ex.Message);
            }
        }

        return anyDeferred
            ? new ImageActionResult(ImageOutcome.Deferred, "finishes when Windows first starts")
            : new ImageActionResult(ImageOutcome.Applied);
    }
}
