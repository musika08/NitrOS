using NitrOS.Core.Model;

namespace NitrOS.Core.Loading;

public record CatalogError(string File, long? Line, string? SwitchId, string Message);

public record CatalogLoadResult(Catalog? Catalog, IReadOnlyList<CatalogError> Errors)
{
    public bool Ok => Errors.Count == 0;
}
