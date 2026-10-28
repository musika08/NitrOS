using CommunityToolkit.Mvvm.ComponentModel;
using NitrOS.App.Logic.Navigation;
using NitrOS.Core.Loading;

namespace NitrOS.App.Logic.Screens;

public sealed class BlockedViewModel : ObservableObject, IScreen
{
    private IReadOnlyList<string> _errors = [];

    public ScreenKey Key => ScreenKeys.Blocked;

    public string Title => "NitrOS cannot open";
    public string Message => "The switch catalog could not be loaded because of the following errors:";

    public IReadOnlyList<string> Errors
    {
        get => _errors;
        private set => SetProperty(ref _errors, value);
    }

    public void OnShown(object? argument)
    {
        if (argument is IReadOnlyList<CatalogError> catalogErrors)
        {
            Errors = catalogErrors
                .Select(e => string.IsNullOrEmpty(e.SwitchId) ? $"{e.File}: {e.Message}" : $"{e.File} ({e.SwitchId}): {e.Message}")
                .ToList();
        }
        else if (argument is IReadOnlyList<string> strErrors)
        {
            Errors = strErrors;
        }
    }
}
