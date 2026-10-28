using NitrOS.App.Logic.Running;

namespace NitrOS.App.Logic.Navigation;

public sealed class Navigator(RunGate gate)
{
    private readonly RunGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    private readonly Dictionary<ScreenKey, Func<IScreen>> _factories = [];
    private readonly Stack<IScreen> _history = new();

    public event EventHandler<IScreen>? Navigated;

    public IScreen? Current => _history.Count > 0 ? _history.Peek() : null;

    public bool CanGoBack => _history.Count > 1;

    public void Register(ScreenKey key, Func<IScreen> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_factories.TryAdd(key, factory))
            throw new InvalidOperationException($"Screen '{key.Name}' is already registered.");
    }

    public bool IsRegistered(ScreenKey key) => _factories.ContainsKey(key);

    public void GoTo(ScreenKey key, object? argument = null)
    {
        if (_gate.IsBusy) return;
        if (!_factories.TryGetValue(key, out var factory))
            throw new InvalidOperationException($"Screen '{key.Name}' is not registered.");

        var screen = factory();
        _history.Push(screen);
        screen.OnShown(argument);
        Navigated?.Invoke(this, screen);
    }

    public void ResetTo(ScreenKey key, object? argument = null)
    {
        if (_gate.IsBusy && key != ScreenKeys.Done) return;
        if (!_factories.TryGetValue(key, out var factory))
            throw new InvalidOperationException($"Screen '{key.Name}' is not registered.");

        _history.Clear();
        var screen = factory();
        _history.Push(screen);
        screen.OnShown(argument);
        Navigated?.Invoke(this, screen);
    }

    public void GoBack()
    {
        if (_gate.IsBusy) return;
        if (_history.Count <= 1) return;

        _history.Pop();
        var previous = _history.Peek();
        Navigated?.Invoke(this, previous);
    }
}
