using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NitrOS.App.Logic.Ports;

namespace NitrOS.App;

public sealed class ContentDialogs(Window window) : IDialogs
{
    private readonly Window _window = window ?? throw new ArgumentNullException(nameof(window));

    public Task<bool> ConfirmAsync(ConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tcs = new TaskCompletionSource<bool>();

        _window.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var panel = new StackPanel { Spacing = 12 };
                if (!string.IsNullOrWhiteSpace(request.Message))
                {
                    panel.Children.Add(new TextBlock { Text = request.Message, TextWrapping = TextWrapping.Wrap });
                }

                if (request.Items is { Count: > 0 })
                {
                    var scroll = new ScrollViewer { MaxHeight = 240 };
                    var itemsList = new ItemsControl { ItemsSource = request.Items };
                    scroll.Content = itemsList;
                    panel.Children.Add(scroll);
                }

                var dialog = new ContentDialog
                {
                    Title = request.Title,
                    Content = panel,
                    PrimaryButtonText = request.ConfirmText,
                    CloseButtonText = request.CancelText,
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = _window.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                tcs.SetResult(result == ContentDialogResult.Primary);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    public Task ShowAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource();

        _window.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = _window.Content.XamlRoot
                };

                await dialog.ShowAsync();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}
