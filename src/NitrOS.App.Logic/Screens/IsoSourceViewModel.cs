namespace NitrOS.App.Logic.Screens;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Storage;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Image.Building;
using NitrOS.Live.Systems;

public sealed record EditionChoice(int Index, string Text, bool Usable, string? Why);

public sealed class IsoSourceViewModel : ObservableObject, IScreen
{
    private readonly Navigator _navigator;
    private readonly Draft _draft;
    private readonly IIsoBackend _backend;
    private readonly IFilePicker _picker;
    private readonly IDialogs _dialogs;
    private readonly IsoBuildIndex _index;
    private readonly IRegistry _registry;

    private string? _sourcePath;
    private string? _outputPath;
    private IReadOnlyList<EditionChoice> _editions = [];
    private EditionChoice? _chosen;
    private string? _problem;
    private string? _resumeText;
    private string? _shadowDefenderNote;
    private IsoBuildRecord? _unfinished;

    public ScreenKey Key => ScreenKeys.IsoSource;

    public string? SourcePath
    {
        get => _sourcePath;
        set { if (SetProperty(ref _sourcePath, value)) UpdateContinueState(); }
    }

    public string? OutputPath
    {
        get => _outputPath;
        set { if (SetProperty(ref _outputPath, value)) UpdateContinueState(); }
    }

    public IReadOnlyList<EditionChoice> Editions
    {
        get => _editions;
        private set => SetProperty(ref _editions, value);
    }

    public EditionChoice? Chosen
    {
        get => _chosen;
        set { if (SetProperty(ref _chosen, value)) UpdateContinueState(); }
    }

    public string? Problem
    {
        get => _problem;
        set => SetProperty(ref _problem, value);
    }

    public string? ResumeText
    {
        get => _resumeText;
        set => SetProperty(ref _resumeText, value);
    }

    public string AlwaysNote => "NitrOS never changes your ISO. It writes a new file.";

    public string? ShadowDefenderNote
    {
        get => _shadowDefenderNote;
        private set => SetProperty(ref _shadowDefenderNote, value);
    }

    public IAsyncRelayCommand PickSource { get; }
    public IAsyncRelayCommand PickOutput { get; }
    public IRelayCommand Continue { get; }
    public IRelayCommand ResumeBuild { get; }
    public IAsyncRelayCommand ThrowAway { get; }
    public IRelayCommand Back { get; }

    public IsoSourceViewModel(
        Navigator navigator,
        Draft draft,
        IIsoBackend backend,
        IFilePicker picker,
        IDialogs dialogs,
        IsoBuildIndex index,
        IRegistry registry)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        PickSource = new AsyncRelayCommand(ExecutePickSourceAsync);
        PickOutput = new AsyncRelayCommand(ExecutePickOutputAsync);
        Continue = new RelayCommand(ExecuteContinue, CanContinue);
        ResumeBuild = new RelayCommand(ExecuteResumeBuild);
        ThrowAway = new AsyncRelayCommand(ExecuteThrowAwayAsync);
        Back = new RelayCommand(() => _navigator.GoTo(ScreenKeys.Start));
    }

    public void OnShown(object? argument)
    {
        // Check shadow defender
        if (_registry.KeyExists(LiveHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\diskpt", RegView.Default))
        {
            ShadowDefenderNote = "Shadow Defender is installed. If the drive you save to is in Shadow Mode, the build is lost at restart.";
        }
        else
        {
            ShadowDefenderNote = null;
        }

        // Check unfinished build
        _unfinished = _index.Read();
        if (_unfinished != null)
        {
            var cardName = _unfinished.Card?.ToString() ?? "custom";
            ResumeText = $"An ISO build was stopped before it finished ({cardName}, saved to {_unfinished.OutputIso}). Resume it, or throw it away.";
        }
        else
        {
            ResumeText = null;
        }

        UpdateContinueState();
    }

    private async Task ExecutePickSourceAsync()
    {
        var path = await _picker.PickIsoToOpenAsync();
        if (string.IsNullOrWhiteSpace(path)) return;

        SourcePath = path;
        Problem = "Reading the editions in this ISO...";
        Editions = [];
        Chosen = null;

        try
        {
            var eds = await Task.Run(() => _backend.Editions(path));
            var choices = new List<EditionChoice>();
            foreach (var ed in eds)
            {
                var isArm = ed.Architecture.Contains("arm", StringComparison.OrdinalIgnoreCase);
                var isOld = ed.Version < BuildChecks.MinImage;
                bool usable = !isArm && !isOld;
                string? why = null;
                if (isArm) why = "ARM64 images are not supported.";
                else if (isOld) why = $"Windows build {ed.Version.Build} is older than required {BuildChecks.MinImage.Build}.";

                var desc = $"{ed.Name} ({ed.EditionId}, {ed.Architecture})";
                choices.Add(new EditionChoice(ed.Index, desc, usable, why));
            }

            Editions = choices;
            Problem = null;

            // Select Pro by default, else first usable
            Chosen = choices.FirstOrDefault(c => c.Usable && c.Text.Contains("Pro", StringComparison.OrdinalIgnoreCase))
                     ?? choices.FirstOrDefault(c => c.Usable);
        }
        catch (Exception ex)
        {
            Problem = ex.Message;
        }

        UpdateContinueState();
    }

    private async Task ExecutePickOutputAsync()
    {
        var suggested = SourcePath != null
            ? Path.GetFileNameWithoutExtension(SourcePath) + "-nitros.iso"
            : "windows-11-nitros.iso";

        var path = await _picker.PickIsoToSaveAsync(suggested);
        if (!string.IsNullOrWhiteSpace(path))
        {
            OutputPath = path;
        }
    }

    private bool CanContinue()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(OutputPath)) return false;
        if (Chosen == null || !Chosen.Usable) return false;

        var req = new BuildRequest(SourcePath, OutputPath, Chosen.Index, [], null);
        var reason = _backend.PathsReason(req);
        return reason == null;
    }

    private void UpdateContinueState()
    {
        Continue.NotifyCanExecuteChanged();
    }

    private void ExecuteContinue()
    {
        if (!CanContinue() || Chosen == null || SourcePath == null || OutputPath == null) return;

        _draft.Iso = new IsoPaths(SourcePath, OutputPath)
        {
            EditionIndex = Chosen.Index
        };

        _navigator.GoTo(ScreenKeys.Cards);
    }

    private void ExecuteResumeBuild()
    {
        if (_unfinished == null) return;

        _draft.Iso = new IsoPaths("", _unfinished.OutputIso);
        if (_unfinished.Card != null)
        {
            _draft.PickCard(_unfinished.Card.Value);
        }
        else
        {
            _draft.UseIds(_unfinished.SwitchIds);
        }

        var req = new RunRequest(RunKind.Iso, _draft.Selection!, _unfinished.Card, false, _draft.Iso);
        _navigator.GoTo(ScreenKeys.Working, req);
    }

    private async Task ExecuteThrowAwayAsync()
    {
        if (_unfinished == null) return;

        var confirmed = await _dialogs.ConfirmAsync(new ConfirmRequest(
            Title: "Throw away unfinished build?",
            Message: "Throw away the unfinished build? Its work files are deleted. Your source ISO is not touched.",
            Items: [],
            ConfirmText: "Throw away",
            CancelText: "Keep it"));

        if (!confirmed) return;

        var resumable = _backend.FindResumable(_unfinished.OutputIso);
        if (resumable != null)
        {
            _backend.Discard(resumable);
        }

        _index.Clear();
        _unfinished = null;
        ResumeText = null;
    }
}
