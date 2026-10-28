using NitrOS.Core.Model;

namespace NitrOS.App.Logic.Screens;

public sealed record CardItem(
    CardId Id,
    string Title,
    string MemoryText,
    string ProcessesText,
    string StopsText,
    string? PartlyNote)
{
    public bool IsRecommended => Id == CardId.Recommended;
}
