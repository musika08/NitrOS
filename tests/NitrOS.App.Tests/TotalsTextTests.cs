using NitrOS.App.Logic.Formatting;
using NitrOS.Core.Selecting;

namespace NitrOS.App.Tests;

public sealed class TotalsTextTests
{
    [Fact]
    public void TotalsText_AllThreeShapes()
    {
        var whole = new Totals(1229, 23, true, 0, []);
        Assert.Equal("About 1.2 GB less memory in use and 23 fewer processes (measured for the whole card, estimate)", TotalsText.For(whole));

        var partial = new Totals(1229, 23, false, 12, []);
        Assert.Equal("About 1.2 GB less memory in use and 23 fewer processes (estimate), 12 switches not measured yet", TotalsText.For(partial));

        var nothing = new Totals(0, 0, false, 5, []);
        Assert.Equal("Memory saved: not measured yet (5 switches)", TotalsText.For(nothing));
    }
}
