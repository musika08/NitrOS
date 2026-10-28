using CommunityToolkit.Mvvm.Input;
using NitrOS.App.Logic.Running;
using NitrOS.App.Tests.Fakes;

namespace NitrOS.App.Tests;

public sealed class RunGateTests
{
    [Fact]
    public void RunGate_DisablesChangeCommandsWhileBusy()
    {
        var gate = new RunGate();
        var probe = new FakeLockProbe { IsInProgress = false };

        var command = new RelayCommand(() => { }, () => gate.CanChange(probe));
        gate.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RunGate.IsBusy))
                command.NotifyCanExecuteChanged();
        };

        Assert.True(command.CanExecute(null));

        var releaser = gate.TryEnter();
        Assert.NotNull(releaser);
        Assert.False(command.CanExecute(null));

        releaser.Dispose();
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RunGate_DisabledWhenLockHeldElsewhere()
    {
        var gate = new RunGate();
        var probe = new FakeLockProbe { IsInProgress = true };

        Assert.False(gate.CanChange(probe));

        probe.IsInProgress = false;
        Assert.True(gate.CanChange(probe));
    }
}
