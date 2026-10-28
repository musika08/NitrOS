using CommunityToolkit.Mvvm.ComponentModel;
using NitrOS.App.Logic.Ports;

namespace NitrOS.App.Logic.Running;

public sealed class RunGate : ObservableObject
{
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public IDisposable? TryEnter()
    {
        if (_isBusy) return null;
        IsBusy = true;
        return new Releaser(this);
    }

    public bool CanChange(ITuneLockProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        return !_isBusy && !probe.IsRunInProgress();
    }

    private sealed class Releaser(RunGate gate) : IDisposable
    {
        private RunGate? _gate = gate;

        public void Dispose()
        {
            var g = Interlocked.Exchange(ref _gate, null);
            if (g is not null)
                g.IsBusy = false;
        }
    }
}
