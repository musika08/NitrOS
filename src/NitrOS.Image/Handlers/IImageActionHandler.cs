namespace NitrOS.Image.Handlers;

using NitrOS.Core.Model;
using NitrOS.Image.Building;

public sealed record ImageActionResult(ImageOutcome Outcome, string? Detail = null);

public interface IImageActionHandler
{
    ImageActionResult Apply(SwitchAction action, ImageContext ctx);
}
