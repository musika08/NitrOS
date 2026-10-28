using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NitrOS.App.Logic.Screens;
using NitrOS.Core.Model;

namespace NitrOS.App.Pages;

public sealed partial class CardsPage : Page
{
    public CardsViewModel? ViewModel => DataContext as CardsViewModel;

    public CardsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is CardsViewModel vm)
        {
            DataContext = vm;
        }
    }

    private void OnPickClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CardId cardId } && ViewModel is not null)
        {
            ViewModel.Pick.Execute(cardId);
        }
    }
}
