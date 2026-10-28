using NitrOS.App.Logic.Help;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;

namespace NitrOS.App.Tests;

public sealed class MythsTests
{
    private static readonly string[] ExpectedNames =
    [
        "SysMain off",
        "memory compression off",
        "pagefile off",
        "timer resolution",
        "Nagle and TcpAckFrequency",
        "NetworkThrottlingIndex and SystemResponsiveness",
        "Win32PrioritySeparation",
        "MSI mode",
        "MPO off",
        "core parking",
        "HPET and bcdedit timer tweaks",
        "Spectre and Meltdown mitigations off",
        "Office Click-to-Run off",
        "touch keyboard service off or TextInputHost rename",
        "Error Reporting off"
    ];

    [Fact]
    public void Myths_ListsEveryNotShippedItemInOrder()
    {
        var items = Myths.All;
        Assert.Equal(ExpectedNames.Length, items.Count);
        for (var i = 0; i < ExpectedNames.Length; i++)
        {
            Assert.Equal(ExpectedNames[i], items[i].Name);
        }
    }

    [Fact]
    public void Myths_EveryItemHasVerdictAndOneSentence()
    {
        var items = Myths.All;
        Assert.NotEmpty(items);

        foreach (var item in items)
        {
            Assert.Contains(item.Verdict, (string[])["No effect", "Harmful"]);
            Assert.False(string.IsNullOrWhiteSpace(item.Why), $"Empty reason for {item.Name}");
            Assert.True(item.Why.EndsWith('.'), $"Reason for {item.Name} must end with a full stop: {item.Why}");
            Assert.DoesNotContain("—", item.Why);
            Assert.DoesNotContain("--", item.Why);
            // Single sentence: exactly one period at the end
            Assert.Equal(1, item.Why.Count(c => c == '.'));
        }
    }

    [Fact]
    public void MythsViewModel_ExposesIntroAllAndBack()
    {
        var nav = new Navigator(new RunGate());
        var vm = new MythsViewModel(nav);

        Assert.Equal(ScreenKeys.Myths, vm.Key);
        Assert.Equal("These popular tweaks are not in NitrOS, because testing shows they do nothing on Windows 11 or cause harm.", vm.Intro);
        Assert.Equal(15, vm.All.Count);

        nav.Register(ScreenKeys.Start, () => new DummyScreen(ScreenKeys.Start));
        nav.Register(ScreenKeys.Myths, () => vm);
        nav.GoTo(ScreenKeys.Start);
        nav.GoTo(ScreenKeys.Myths);
        Assert.Equal(ScreenKeys.Myths, nav.Current?.Key);

        vm.Back.Execute(null);
        Assert.Equal(ScreenKeys.Start, nav.Current?.Key);
    }

    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public void OnShown(object? argument) { }
    }
}
