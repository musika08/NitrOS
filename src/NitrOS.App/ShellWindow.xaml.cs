using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace NitrOS.App;

public sealed partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
        Title = "NitrOS";
        SystemBackdrop = new MicaBackdrop();
    }
}
