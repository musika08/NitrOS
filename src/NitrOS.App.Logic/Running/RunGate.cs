using CommunityToolkit.Mvvm.ComponentModel;
using NitrOS.App.Logic.Ports;

namespace NitrOS.App.Logic.Running;

public sealed class RunGate : ObservableObject
{
    private bool _isBusy;
    private string? _busyMessage;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? BusyMessage
    {
        get => _busyMessage;
        private set => SetProperty(ref _busyMessage, value);
    }

    public IDisposable? TryEnter() => TryEnter(null);

    public IDisposable? TryEnter(string? closeMessage)
    {
        if (_isBusy) return null;
        _busyMessage = closeMessage;
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
            {
                g.BusyMessage = null;
                g.IsBusy = false;
            }
        }
    }
}
