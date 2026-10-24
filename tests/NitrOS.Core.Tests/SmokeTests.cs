public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads() =>
        Assert.Equal("NitrOS.Core", typeof(NitrOS.Core.Model.Risk).Assembly.GetName().Name);
}
