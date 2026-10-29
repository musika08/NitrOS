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
    private bool _isSelected = true;
    private IAsyncRelayCommand? _install;
    private IAsyncRelayCommand? _getAnyway;
    private IRelayCommand? _openPage;
    private IRelayCommand? _startUpdater;
    private IRelayCommand? _openFolder;
    private IRelayCommand? _copyText;

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
        ? "Installed: Utility / Not detected"
        : $"Installed: {Item.InstalledVersion}";

    public string LatestText
    {
        get
        {
            if (Merged.State == ItemState.PageOnly)
                return $"Check {Item.SourceName}'s site";

            if (Merged.State == ItemState.UpToDate)
            {
                var ver = Item.LatestVersion ?? Item.InstalledVersion;
                return $"Latest: {ver} (Up to date)";
            }

            var sizeStr = Item.SizeBytes.HasValue ? $", {Item.SizeBytes.Value / 1024.0 / 1024.0:F1} MB" : "";
            var dateStr = Item.ReleaseDate.HasValue ? $", {Item.ReleaseDate.Value:d MMM yyyy}" : "";
            return $"Latest: {Item.LatestVersion} from {Item.SourceName}{dateStr}{sizeStr}";
        }
    }

    public string StateText => Merged.State switch
    {
        ItemState.UpToDate => "Up to date",
        ItemState.NewVersion => "New version",
        ItemState.CantTell => "Utility",
        ItemState.Blocked => "Blocked",
        ItemState.PageOnly => "Maker's site",
        _ => "Unknown"
    };

    public bool IsUpToDate => Merged.State == ItemState.UpToDate;
    public bool IsNewVersion => Merged.State == ItemState.NewVersion;
    public bool IsBlocked => Merged.State == ItemState.Blocked;
    public bool IsUtility => Merged.State == ItemState.CantTell || Merged.State == ItemState.PageOnly;

    public string? BlockedReason => Merged.BlockedReason;

    public string? AlsoOfferedText => Merged.AlsoOfferedBy.Count > 0
        ? $"Also offered by {Merged.AlsoOfferedBy[0].SourceName}: {Merged.AlsoOfferedBy[0].LatestVersion}"
        : null;

    public bool IsFirmware => Item.Kind == UpdateKind.Bios || Item.Kind == UpdateKind.Firmware;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasAction => (Install != null || GetAnyway != null) && StartUpdater == null;
    public bool ShowInstall => Install != null && StartUpdater == null;
    public bool ShowGetAnyway => GetAnyway != null && StartUpdater == null;

    public IAsyncRelayCommand? Install
    {
        get => _install;
        set
        {
            if (SetProperty(ref _install, value))
            {
                OnPropertyChanged(nameof(ShowInstall));
                OnPropertyChanged(nameof(HasAction));
            }
        }
    }

    public IAsyncRelayCommand? GetAnyway
    {
        get => _getAnyway;
        set
        {
            if (SetProperty(ref _getAnyway, value))
            {
                OnPropertyChanged(nameof(ShowGetAnyway));
                OnPropertyChanged(nameof(HasAction));
            }
        }
    }

    public IRelayCommand? OpenPage
    {
        get => _openPage;
        set => SetProperty(ref _openPage, value);
    }

    public IRelayCommand? StartUpdater
    {
        get => _startUpdater;
        set
        {
            if (SetProperty(ref _startUpdater, value))
            {
                OnPropertyChanged(nameof(ShowInstall));
                OnPropertyChanged(nameof(ShowGetAnyway));
                OnPropertyChanged(nameof(HasAction));
            }
        }
    }

    public IRelayCommand? OpenFolder
    {
        get => _openFolder;
        set => SetProperty(ref _openFolder, value);
    }

    public IRelayCommand? CopyText
    {
        get => _copyText;
        set => SetProperty(ref _copyText, value);
    }

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
