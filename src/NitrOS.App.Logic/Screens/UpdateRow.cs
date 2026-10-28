using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.Updates.Merging;
using NitrOS.Updates.Model;

namespace NitrOS.App.Logic.Screens;

public sealed class UpdateRow : ObservableObject
{
    private string? _progress;
    private string? _result;
    private string? _unavailable;

    public MergedItem Merged { get; }
    public UpdateItem Item => Merged.Item;

    public string Name => Item.Name;
    public string KindText => Item.Kind switch
    {
        UpdateKind.Bios => "BIOS",
        UpdateKind.Firmware => "Firmware",
        _ => "Driver"
    };

    public string InstalledText => string.IsNullOrEmpty(Item.InstalledVersion)
        ? "Installed: can't read it on this PC"
        : $"Installed: {Item.InstalledVersion}";

    public string LatestText
    {
        get
        {
            if (Merged.State == ItemState.PageOnly)
                return $"Check {Item.SourceName}'s site";

            var sizeStr = Item.SizeBytes.HasValue ? $", {Item.SizeBytes.Value / 1024.0 / 1024.0:F1} MB" : "";
            var dateStr = Item.ReleaseDate.HasValue ? $", {Item.ReleaseDate.Value:d MMM yyyy}" : "";
            return $"Latest: {Item.LatestVersion} from {Item.SourceName}{dateStr}{sizeStr}";
        }
    }

    public string StateText => Merged.State switch
    {
        ItemState.UpToDate => "Up to date",
        ItemState.NewVersion => "New version",
        ItemState.CantTell => "Can't tell",
        ItemState.Blocked => "Blocked",
        ItemState.PageOnly => "On the maker's site",
        _ => "Unknown"
    };

    public string? BlockedReason => Merged.BlockedReason;

    public string? AlsoOfferedText => Merged.AlsoOfferedBy.Count > 0
        ? $"Also offered by {Merged.AlsoOfferedBy[0].SourceName}: {Merged.AlsoOfferedBy[0].LatestVersion}"
        : null;

    public bool IsFirmware => Item.Kind == UpdateKind.Bios || Item.Kind == UpdateKind.Firmware;

    public IAsyncRelayCommand? Install { get; set; }
    public IAsyncRelayCommand? GetAnyway { get; set; }
    public IRelayCommand? OpenPage { get; set; }
    public IRelayCommand? StartUpdater { get; set; }
    public IRelayCommand? OpenFolder { get; set; }
    public IRelayCommand? CopyText { get; set; }

    public string? Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string? Result
    {
        get => _result;
        set => SetProperty(ref _result, value);
    }

    public string? Unavailable
    {
        get => _unavailable;
        set => SetProperty(ref _unavailable, value);
    }

    public UpdateRow(MergedItem merged)
    {
        Merged = merged ?? throw new ArgumentNullException(nameof(merged));
    }
}
