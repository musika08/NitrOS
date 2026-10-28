using NitrOS.Core.Selecting;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.App.Logic.Running;

public sealed class LiveTuneBackend(Func<LiveSystems> systems, JournalStore store) : ITuneBackend
{
    private readonly Func<LiveSystems> _systems = systems ?? throw new ArgumentNullException(nameof(systems));
    private readonly JournalStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public TuneResult Apply(Selection selection, TuneOptions options, IProgress<TuneProgress>? progress, CancellationToken ct)
    {
        var engine = new TuneEngine(_systems(), _store);
        return engine.Apply(selection, options, progress, ct);
    }
}

public sealed class LiveTuneBackendFactory(Func<LiveSystems> systems) : ITuneBackendFactory
{
    private readonly Func<LiveSystems> _systems = systems ?? throw new ArgumentNullException(nameof(systems));

    public ITuneBackend Create(JournalStore store) => new LiveTuneBackend(_systems, store);
}
