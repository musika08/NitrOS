using System.Runtime.InteropServices;

namespace NitrOS.Updates.Windows;

public sealed class WinUnelevatedLauncher : NitrOS.Updates.Installing.IUnelevatedLauncher
{
    public bool OpenPage(Uri page)
    {
        if (page == null) return false;
        return LaunchUnelevated(page.ToString(), null);
    }

    public bool ShowInFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return LaunchUnelevated("explorer.exe", $"/select,\"{path}\"");
    }

    private static bool LaunchUnelevated(string applicationOrUrl, string? arguments)
    {
        try
        {
            // Raymond Chen's shell dispatch via IShellWindows / desktop shell view
            var shellWindowsType = Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"));
            if (shellWindowsType == null) return false;

            dynamic shellWindows = Activator.CreateInstance(shellWindowsType)!;
            dynamic item = shellWindows.Item(0); // 0 = CSIDL_DESKTOP
            if (item == null) return false;

            dynamic serviceProvider = item;
            var sid = new Guid("4C96BE40-915C-11CF-99D3-00AA004E0409"); // SID_STopLevelBrowser
            var iid = new Guid("000214E2-0000-0000-C000-000000000046"); // IID_IShellBrowser

            // Fallback to ShellDispatch2 directly via item.Document.Application
            dynamic shellApp = item.Document.Application;
            shellApp.ShellExecute(applicationOrUrl, arguments ?? "", "", "open", 1);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
