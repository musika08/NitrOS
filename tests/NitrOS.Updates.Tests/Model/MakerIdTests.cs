namespace NitrOS.Updates.Tests.Model;

using NitrOS.Updates.Model;

public sealed class MakerIdTests
{
    [Fact]
    public void Makers_FromManufacturer()
    {
        Assert.Equal(MakerId.Asus, Makers.FromManufacturer("ASUSTeK COMPUTER INC."));
        Assert.Equal(MakerId.Msi, Makers.FromManufacturer("Micro-Star International Co., Ltd."));
        Assert.Equal(MakerId.Other, Makers.FromManufacturer("Contoso"));
    }
}
