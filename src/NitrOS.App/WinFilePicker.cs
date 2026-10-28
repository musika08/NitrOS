namespace NitrOS.App;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using NitrOS.App.Logic.Ports;
using Windows.Storage.Pickers;
using WinRT.Interop;

public sealed class WinFilePicker : IFilePicker
{
    private readonly Window _window;

    public WinFilePicker(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public async Task<string?> PickIsoToOpenAsync()
    {
        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(_window);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".iso");
        picker.SuggestedStartLocation = PickerLocationId.Downloads;

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickIsoToSaveAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        var hwnd = WindowNative.GetWindowHandle(_window);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeChoices.Add("ISO Disc Image", [".iso"]);
        picker.SuggestedFileName = suggestedName;
        picker.SuggestedStartLocation = PickerLocationId.Downloads;

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
