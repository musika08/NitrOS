using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Live;
using NitrOS.Live.Windows;

namespace NitrOS.App.Logic.Storage;

public sealed class AppStateStore(
    string folder,
    Func<string, FolderState> check,
    Action<string> ensure,
    ISessionFacts session)
{
    private readonly string _folder = folder ?? throw new ArgumentNullException(nameof(folder));
    private readonly Func<string, FolderState> _check = check ?? throw new ArgumentNullException(nameof(check));
    private readonly Action<string> _ensure = ensure ?? throw new ArgumentNullException(nameof(ensure));
    private readonly ISessionFacts _session = session ?? throw new ArgumentNullException(nameof(session));

    public AppState Read()
    {
        var file = Path.Combine(_folder, "state.json");
        if (!Directory.Exists(_folder) || !File.Exists(file))
            return new AppState(null, false, null);

        try
        {
            var text = File.ReadAllText(file);
            return JsonSerializer.Deserialize<AppState>(text, CatalogJson.Options) ?? new AppState(null, false, null);
        }
        catch
        {
            return new AppState(null, false, null);
        }
    }

    private void Write(Func<AppState, AppState> update)
    {
        if (!_session.IsElevated) throw new InvalidOperationException(Texts.NotElevated);
        _ensure(_folder);
        Directory.CreateDirectory(_folder);
        var current = Read();
        var next = update(current);
        var file = Path.Combine(_folder, "state.json");
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(next, CatalogJson.Options));
        File.Move(tmp, file, overwrite: true);
    }

    public void SetHealthPending(HealthPending hp) =>
        Write(s => new AppState(hp, s.CheckAfterUpdates, s.LastCheckedBuild));

    public void ClearHealthPending() =>
        Write(s => new AppState(null, s.CheckAfterUpdates, s.LastCheckedBuild));

    public void SetCheckAfterUpdates(bool check) =>
        Write(s => new AppState(s.Health, check, s.LastCheckedBuild));

    public void SetLastCheckedBuild(string? build) =>
        Write(s => new AppState(s.Health, s.CheckAfterUpdates, build));
}
