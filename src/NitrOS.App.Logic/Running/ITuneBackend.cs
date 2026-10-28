using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Journal;

namespace NitrOS.App.Logic.Running;

public interface ITuneBackend
{
    TuneResult Apply(Selection selection, TuneOptions options, IProgress<TuneProgress>? progress, CancellationToken ct);
}

public interface ITuneBackendFactory
{
    ITuneBackend Create(JournalStore store);
}
