using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Formatting;
using NitrOS.Core.Model;

namespace NitrOS.App.Logic.Screens;

public sealed class SwitchRow : ObservableObject
{
    private bool _isOn;
    private IReadOnlyList<string> _warnings = [];

    public string Id { get; }
    public string Name { get; }
    public string Feature { get; }
    public string Description { get; }
    public string MemoryText { get; }
    public string RiskBadge { get; }
    public bool IsAvailable { get; }
    public string? UnavailableReason { get; }
    public bool CantUndoBadge { get; }
    public IRelayCommand Toggle { get; }

    public bool IsOn
    {
        get => _isOn;
        set => SetProperty(ref _isOn, value);
    }

    public IReadOnlyList<string> Warnings
    {
        get => _warnings;
        set => SetProperty(ref _warnings, value);
    }

    public SwitchRow(
        SwitchDef def,
        bool isOn,
        bool isAvailable,
        string? unavailableReason,
        bool isTuneMode,
        IReadOnlyList<string> warnings,
        Action<string> onToggle)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(onToggle);

        Id = def.Id;
        Name = def.Name;
        Feature = def.Feature;
        Description = def.Description;
        IsOn = isOn;
        IsAvailable = isAvailable;
        UnavailableReason = unavailableReason;
        Warnings = warnings ?? [];

        MemoryText = def.Measured is not null
            ? $"Saves about {Plain.Memory(def.Measured.MemoryMb)} (estimate)"
            : "not measured yet";

        RiskBadge = def.Risk switch
        {
            Risk.Safe => "Safe",
            Risk.Caution => "Caution",
            Risk.Breaks => "Breaks something",
            _ => ""
        };

        CantUndoBadge = isTuneMode && !def.Reversible;

        Toggle = new RelayCommand(() =>
        {
            if (IsAvailable)
                onToggle(Id);
        });
    }
}
