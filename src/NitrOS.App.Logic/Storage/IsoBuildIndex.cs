namespace NitrOS.App.Logic.Storage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NitrOS.Core.Model;
using NitrOS.Live;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

public sealed record IsoBuildRecord(
    string OutputIso,
    string WorkFolder,
    string BuildId,
    CardId? Card,
    IReadOnlyList<string> SwitchIds);

public sealed class IsoBuildIndex
{
    private readonly string _folder;
    private readonly Func<string, FolderState> _check;
    private readonly Action<string> _ensure;
    private readonly ISessionFacts _session;

    public IsoBuildIndex(
        string folder,
        Func<string, FolderState> check,
        Action<string> ensure,
        ISessionFacts session)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        _check = check ?? throw new ArgumentNullException(nameof(check));
        _ensure = ensure ?? throw new ArgumentNullException(nameof(ensure));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    private string FilePath => Path.Combine(_folder, "iso-build.json");

    public IsoBuildRecord? Read()
    {
        // Never creates folder or file
        if (!Directory.Exists(_folder) || !File.Exists(FilePath)) return null;

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<IsoBuildRecord>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Set(IsoBuildRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!_session.IsElevated) throw new UnauthorizedAccessException("Administrator rights required.");

        _ensure(_folder);
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    public void Clear()
    {
        if (!_session.IsElevated) throw new UnauthorizedAccessException("Administrator rights required.");
        if (!File.Exists(FilePath)) return;

        try
        {
            File.Delete(FilePath);
        }
        catch
        {
            // ignore
        }
    }
}
