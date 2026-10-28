using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using NitrOS.App.Logic;
using NitrOS.App.Logic.Formatting;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Startup;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using Windows.Gaming.Input;
using Windows.Graphics.Imaging;

namespace NitrOS.App;

public static class SmokeRunner
{
    public static async Task RunAsync(ShellWindow window, AppComposition composition, string outputPath)
    {
        try
        {
            var pagesDir = outputPath + ".pages";
            Directory.CreateDirectory(pagesDir);

            var screensVisited = new List<string>();

            // Helper to delay and capture window content
            async Task CaptureScreenAsync(string name)
            {
                await Task.Delay(350);
                if (window.Content is FrameworkElement fe)
                {
                    var rtb = new RenderTargetBitmap();
                    await rtb.RenderAsync(fe);
                    var pixels = await rtb.GetPixelsAsync();

                    var pngPath = Path.Combine(pagesDir, $"{name}.png");
                    using var fs = File.Create(pngPath);
                    using var ras = fs.AsRandomAccessStream();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ras);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        (uint)rtb.PixelWidth,
                        (uint)rtb.PixelHeight,
                        96, 96,
                        pixels.ToArray());
                    await encoder.FlushAsync();
                }

                if (!screensVisited.Contains(name))
                {
                    screensVisited.Add(name);
                }
            }

            // 1. Start screen
            composition.Navigator.GoTo(ScreenKeys.Start);
            await CaptureScreenAsync("Start");

            // 2. Tune walk: Cards -> Pick Recommended -> Switches -> Review
            composition.Draft.Start(TargetMode.TunePc);
            composition.Navigator.GoTo(ScreenKeys.Cards);
            await CaptureScreenAsync("Cards-Tune");

            composition.Draft.PickCard(CardId.Recommended);
            composition.Navigator.GoTo(ScreenKeys.Switches);
            await CaptureScreenAsync("Switches-Tune");

            // Gamepad check on Switches page
            var gamepads = Gamepad.Gamepads;
            bool? dpadMovedFocus = null;
            if (gamepads.Count > 0)
            {
                // Wait briefly for owner input if requested
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.Elapsed.TotalSeconds < 2)
                {
                    await Task.Delay(100);
                    var reading = gamepads[0].GetCurrentReading();
                    if (reading.Buttons.HasFlag(GamepadButtons.DPadDown))
                    {
                        dpadMovedFocus = true;
                        break;
                    }
                }
            }

            composition.Navigator.GoTo(ScreenKeys.Review, composition.Draft);
            await CaptureScreenAsync("Review-Tune");

            var reviewTuneVm = composition.Navigator.Current as ReviewViewModel;
            bool reviewGoEnabledTune = reviewTuneVm?.Go.CanExecute(null) ?? false;

            // 3. ISO walk: Cards -> Pick Recommended -> Switches -> Review
            composition.Draft.Start(TargetMode.BuildIso);
            composition.Navigator.GoTo(ScreenKeys.Cards);
            await CaptureScreenAsync("Cards-Iso");

            composition.Draft.PickCard(CardId.Recommended);
            composition.Navigator.GoTo(ScreenKeys.Switches);
            await CaptureScreenAsync("Switches-Iso");

            composition.Navigator.GoTo(ScreenKeys.Review, composition.Draft);
            await CaptureScreenAsync("Review-Iso");

            var reviewIsoVm = composition.Navigator.Current as ReviewViewModel;
            string? reviewGoReasonIso = reviewIsoVm?.GoUnavailableReason;

            // 4. Device updates walk
            if (composition.Navigator.IsRegistered(ScreenKeys.DeviceUpdates))
            {
                composition.Navigator.GoTo(ScreenKeys.DeviceUpdates);
                await CaptureScreenAsync("DeviceUpdates");
            }

            // 5. Build JSON report
            var report = new SmokeReport(
                CatalogSwitches: composition.Catalog?.Switches.Count ?? 0,
                Device: composition.AppContext.Device.ToString().ToLowerInvariant(),
                SessionOk: composition.AppContext.Session.Ok,
                SessionReason: composition.AppContext.Session.Reason,
                MemoryLine: Plain.MemoryLine(composition.AppContext.Memory),
                Screens: ["Start", "Cards", "Switches", "Review"],
                ReviewGoEnabledTune: reviewGoEnabledTune,
                ReviewGoReasonIso: reviewGoReasonIso,
                Gamepads: gamepads.Count,
                DpadMovedFocus: dpadMovedFocus);

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json);

            await Task.Delay(200);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            var errJson = JsonSerializer.Serialize(new { error = ex.ToString() }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, errJson);
            Environment.Exit(1);
        }
    }
}
