namespace NitrOS.Image.Handlers;

using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.Offline;

public sealed class FileRemoveImageHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var fr = (FileRemove)action;
        string expanded;
        try
        {
            expanded = ImagePaths.Expand(fr.Path, ctx.MountDir);
        }
        catch
        {
            return new ImageActionResult(ImageOutcome.Failed, "failed to expand path");
        }

        if (!ImagePaths.IsUnder(expanded, ctx.MountDir))
        {
            return new ImageActionResult(ImageOutcome.Failed, "path outside mount folder");
        }

        if (!ctx.Systems.Files.Exists(expanded))
        {
            return new ImageActionResult(ImageOutcome.Skipped, "not in this image");
        }

        try
        {
            ctx.Systems.Files.DeleteUnder(ctx.MountDir, expanded);
            return new ImageActionResult(ImageOutcome.Applied);
        }
        catch (System.Exception ex)
        {
            return new ImageActionResult(ImageOutcome.Failed, ex.Message);
        }
    }
}
