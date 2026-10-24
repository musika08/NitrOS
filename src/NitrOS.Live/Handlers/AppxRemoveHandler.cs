using System.Runtime.InteropServices;
using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>
/// Removes a Store app for all users, then deprovisions it. Irreversible: the user reinstalls from the Store.
/// When Windows refuses the built-in (provisioned) app list with access denied, the removal for users still runs, the
/// deprovision part is skipped, and the action is Applied with <see cref="ProvisionRefusedDetail"/>.
/// </summary>
public sealed class AppxRemoveHandler : IActionHandler
{
    public const string ProvisionRefusedDetail = "Windows refused to change the built-in app list";

    private const int EAccessDenied = unchecked((int)0x80070005);

    /// <summary>Access denied anywhere in the exception chain (UnauthorizedAccessException or HRESULT 0x80070005).</summary>
    internal static bool IsAccessDenied(Exception? e)
    {
        for (; e is not null; e = e.InnerException)
            if (e is UnauthorizedAccessException || e.HResult == EAccessDenied
                || (e is COMException c && c.ErrorCode == EAccessDenied))
                return true;
        return false;
    }

    /// <summary>Whether the app is provisioned; null when Windows refuses to say.</summary>
    private static bool? Provisioned(AppxRemove a, LiveSystems s)
    {
        try { return s.Appx.IsProvisioned(a.Name); }
        catch (Exception e) when (IsAccessDenied(e)) { return null; }
    }

    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var a = (AppxRemove)action;
        return s.Appx.IsInstalled(a.Name) || Provisioned(a, s) == true
            ? new IrreversiblePrior(a.Name, null, "Reinstall from the Microsoft Store")
            : new MissingPrior();
    }

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var a = (AppxRemove)action;
        var installed = s.Appx.IsInstalled(a.Name);
        var provisioned = Provisioned(a, s);
        if (!installed && provisioned != true)
            return ApplyResult.Skipped(provisioned is null ? "app not installed; " + ProvisionRefusedDetail : "app not installed");

        if (installed) s.Appx.RemoveForAllUsers(a.Name);
        if (provisioned is null) return new ApplyResult(ActionOutcome.Applied, ProvisionRefusedDetail);
        if (provisioned == true)
        {
            try { s.Appx.Deprovision(a.Name); }
            catch (Exception e) when (IsAccessDenied(e)) { return new ApplyResult(ActionOutcome.Applied, ProvisionRefusedDetail); }
        }
        return ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s) { }

    /// <summary>In effect when the app is installed for no user and is not provisioned (a refused provisioned query counts as not).</summary>
    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (AppxRemove)action;
        return !s.Appx.IsInstalled(a.Name) && Provisioned(a, s) != true;
    }
}
