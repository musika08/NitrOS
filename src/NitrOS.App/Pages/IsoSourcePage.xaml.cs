namespace NitrOS.App.Pages;

using Microsoft.UI.Xaml.Controls;
using NitrOS.App.Logic.Screens;

public sealed partial class IsoSourcePage : Page
{
    public IsoSourceViewModel ViewModel { get; }

    public IsoSourcePage(IsoSourceViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
    }
}
