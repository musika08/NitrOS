using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Formatting;
using NitrOS.Core.Model;

namespace NitrOS.App.Logic.Screens;

/// <summary>Whether a switch's setting is already in effect on this PC (None when building an ISO).</summary>
public enum AppliedState { None, Applied, NotApplied }

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
    public bool CurrentlyInEffect { get; }
    public AppliedState Applied { get; }
    public IRelayCommand Toggle { get; }

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (SetProperty(ref _isOn, value))
            {
                OnPropertyChanged(nameof(ActionBadge));
                OnPropertyChanged(nameof(ActionDetail));
            }
        }
    }

    public string ActionBadge
    {
        get
        {
            bool isTurnOnSwitch = Id == "svchost-grouping-on";
            if (IsOn)
            {
                if (CurrentlyInEffect)
                    return isTurnOnSwitch ? "Already ON" : "Already OFF";
                return isTurnOnSwitch ? "Will turn ON" : "Will turn OFF";
            }
            else
            {
                if (CurrentlyInEffect)
                    return isTurnOnSwitch ? "Currently ON" : "Currently OFF";
                return isTurnOnSwitch ? "Keeps OFF" : "Keeps ON";
            }
        }
    }

    public string ActionDetail
    {
        get
        {
            bool isTurnOnSwitch = Id == "svchost-grouping-on";
            if (IsOn)
            {
                if (CurrentlyInEffect)
                    return "Already applied on this PC";
                return isTurnOnSwitch ? "Setup will enable this" : "Setup will turn this OFF";
            }
            else
            {
                if (CurrentlyInEffect)
                    return "Currently tuned on this PC";
                return isTurnOnSwitch ? "Keeps default (disabled)" : "Setup leaves this ON";
            }
        }
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
        Action<string> onToggle,
        bool currentlyInEffect = false)
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
        CurrentlyInEffect = currentlyInEffect;
        Applied = !isTuneMode ? AppliedState.None : currentlyInEffect ? AppliedState.Applied : AppliedState.NotApplied;
        Warnings = warnings ?? [];

        MemoryText = def.Measured is not null
            ? $"Saves about {Plain.Memory(def.Measured.MemoryMb)} (estimate)"
            : (IsPureSetting(def) ? "Setting tweak" : "Included in card estimate");

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

    private static bool IsPureSetting(SwitchDef def) =>
        def.Live.Count > 0 && def.Live.All(a => a is RegistrySet or RegistryDelete or FeatureDisable or PowerSet);
}
