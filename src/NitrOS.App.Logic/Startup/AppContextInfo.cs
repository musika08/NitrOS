using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live;

namespace NitrOS.App.Logic.Startup;

public sealed record AppContextInfo(
    Catalog? Catalog,
    IReadOnlyList<CatalogError> CatalogErrors,
    DeviceProfile Device,
    MemoryInfo Memory,
    SessionCheck Session)
{
    public string? ConsoleUserSid { get; init; }
}
