using NitrOS.App.Logic.Navigation;

namespace NitrOS.App;

public sealed class PageMap
{
    private readonly Dictionary<ScreenKey, Type> _map = [];

    public void Add(ScreenKey key, Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        _map[key] = pageType;
    }

    public Type For(ScreenKey key) =>
        _map.TryGetValue(key, out var type) ? type : throw new KeyNotFoundException($"No page mapped for screen '{key.Name}'");
}
