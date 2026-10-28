using System.Reflection;
using NitrOS.App.Logic;

namespace NitrOS.App.Tests;

public sealed class ProjectRulesTests
{
    [Fact]
    public void Logic_HasNoWinUiReference()
    {
        AssemblyName[] refs = typeof(Texts).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, a =>
            a.Name != null && (
                a.Name.StartsWith("Microsoft.WinUI", StringComparison.OrdinalIgnoreCase) ||
                a.Name.StartsWith("Microsoft.UI", StringComparison.OrdinalIgnoreCase) ||
                a.Name.StartsWith("Microsoft.WindowsAppSDK", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Logic_ReferencesCoreAndLive()
    {
        var names = typeof(Texts).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null)
            .ToHashSet();

        Assert.Contains("NitrOS.Core", names);
        Assert.Contains("NitrOS.Live", names);
    }
}
