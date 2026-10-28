using NitrOS.Updates.Model;

namespace NitrOS.Updates.Merging;

public sealed record MergedItem(
    UpdateItem Item,
    ItemState State,
    string? BlockedReason,
    IReadOnlyList<UpdateItem> AlsoOfferedBy);
