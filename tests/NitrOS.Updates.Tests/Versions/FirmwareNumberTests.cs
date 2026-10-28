namespace NitrOS.Updates.Tests.Versions;

using NitrOS.Updates.Versions;

public sealed class FirmwareNumberTests
{
    [Fact]
    public void FirmwareNumber_EsrtHex()
    {
        Assert.True(FirmwareNumber.TryEsrtHex("319", out var v1));
        Assert.Equal(793u, v1);

        Assert.True(FirmwareNumber.TryEsrtHex("0015", out var v2));
        Assert.Equal(21u, v2);

        Assert.True(FirmwareNumber.TryEsrtHex("0020", out var v3));
        Assert.Equal(32u, v3);

        Assert.True(FirmwareNumber.TryEsrtHex("10.1.2.20", out var v4));
        Assert.Equal(32u, v4);

        Assert.True(FirmwareNumber.TryEsrtHex("2002", out var v5));
        Assert.Equal(8194u, v5);
    }
}
