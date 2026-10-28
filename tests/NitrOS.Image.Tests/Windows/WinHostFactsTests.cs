namespace NitrOS.Image.Tests.Windows;

using System;
using NitrOS.Image.Windows;

public sealed class WinHostFactsTests
{
    [Fact]
    public void WinHostFacts_ReadsDismVersion()
    {
        var facts = new WinHostFacts();
        Assert.True(facts.DismVersion().Build >= 26100, $"Expected DISM build >= 26100, but got {facts.DismVersion()}");
    }
}
