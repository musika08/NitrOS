namespace NitrOS.Image.Handlers;

using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.FirstBoot;

public sealed class TaskDisableImageHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var td = (TaskDisable)action;
        ctx.FirstBoot.AddMachine(new TaskOff(ctx.SwitchId, td.Path));
        return new ImageActionResult(ImageOutcome.Deferred, "turned off when Windows first starts (no offline way)");
    }
}

public sealed class PowerSetImageHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var ps = (PowerSet)action;
        ctx.FirstBoot.AddMachine(new PowerValue(ctx.SwitchId, ps.Subgroup, ps.Setting, ps.Ac, ps.Dc));
        return new ImageActionResult(ImageOutcome.Deferred, "set when Windows first starts");
    }
}

public sealed class ScriptImageHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var sc = (Script)action;
        ctx.FirstBoot.AddMachine(new ScriptRun(ctx.SwitchId, sc.Body));
        return new ImageActionResult(ImageOutcome.Deferred, "runs when Windows first starts");
    }
}
