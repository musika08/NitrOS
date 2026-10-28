using NitrOS.Updates.Merging;
using NitrOS.Updates.Model;

namespace NitrOS.Updates.Checking;

public sealed record SourceNotice(string SourceName, string Problem, Uri? Page);

public sealed record SupportLink(string Text, Uri Page);

public sealed record CheckProgress(string SourceName, int Done, int Total);

public sealed record CheckReport(
    DeviceInfo Device,
    IReadOnlyList<MergedItem> Items,
    IReadOnlyList<SourceNotice> Notices,
    IReadOnlyList<SupportLink> Links,
    bool WindowsUpdateDriversExcluded,
    DateTimeOffset CheckedAt);
