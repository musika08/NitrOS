namespace NitrOS.Image.Tests.Windows;

using System;
using System.IO;
using NitrOS.Image.Systems;
using NitrOS.Image.Windows;

public sealed class WinImageDismParseTests
{
    private const string ImageInfoOutput = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1

        Details for image : C:\test\install.wim

        Index : 1
        Name : Windows 11 Pro
        Description : Windows 11 Pro
        Size : 4,294,967,296 bytes
        WIM Bootable : No
        Architecture : x64
        Hal : acpiapic
        Version : 10.0.26100.1150
        ServicePack Build : 1150
        Major Version : 10
        Minor Version : 0
        Edition : Professional
        InstallationType : Client

        Index : 2
        Name : Windows 11 Home
        Description : Windows 11 Home
        Size : 4,100,000,000 bytes
        Edition : Core
        Architecture : x64
        Version : 10.0.26100.1
        """;

    private const string MountedImageInfoOutput = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1

        Mounted images:

        Mount Dir : C:\NitrOS-work-1\mount
        Image File : C:\NitrOS-work-1\install.wim
        Image Index : 1
        Mounted Read/Write : Yes
        Status : Ok

        Mount Dir : C:\NitrOS-work-2\mount
        Image File : C:\NitrOS-work-2\install.wim
        Image Index : 1
        Mounted Read/Write : Yes
        Status : Invalid

        Mount Dir : C:\NitrOS-work-3\mount
        Image File : C:\NitrOS-work-3\install.wim
        Image Index : 1
        Mounted Read/Write : Yes
        Status : Needs Remount
        """;

    private const string ProvisionedAppsOutput = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1

        Image information : C:\mount

        Display name : Microsoft.XboxApp
        Version : 1.0.0.0
        Architecture : neutral
        Resource ID : ~
        Package name : Microsoft.XboxApp_1.0.0.0_neutral_~_8wekyb3d8bbwe

        Display name : Microsoft.ZuneVideo
        Version : 2.0.0.0
        Architecture : neutral
        Resource ID : ~
        Package name : Microsoft.ZuneVideo_2.0.0.0_neutral_~_8wekyb3d8bbwe
        """;

    private const string FeatureInfoOutput = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1

        Image information : C:\mount

        Feature Name : MicrosoftWindowsPowerShellV2
        Display Name : Windows PowerShell 2.0
        Description : Windows PowerShell 2.0
        Restart Required : Possible
        State : Enabled
        """;

    private const string PackagesOutput = """
        Deployment Image Servicing and Management tool
        Version: 10.0.26100.1

        Package Identity : Package_for_RollupFix~31bf3856ad364e35~amd64~~26100.1150.1.0
        State : Installed
        Release Type : Update
        Install Time : 10/28/2026

        Package Identity : Package_for_ServicingStack~31bf3856ad364e35~amd64~~26100.1.1.0
        State : Installed
        Release Type : Update
        Install Time : 10/28/2026
        """;

    [Fact]
    public void Parse_EditionsFromImageInfo()
    {
        var indices = WinImageDismParse.ImageIndices(ImageInfoOutput);
        Assert.Equal([1, 2], indices);

        var edition1 = WinImageDismParse.SingleEdition(ImageInfoOutput, 1);
        Assert.Equal("Windows 11 Pro", edition1.Name);
        Assert.Equal("Professional", edition1.EditionId);
        Assert.Equal("x64", edition1.Architecture);
        Assert.Equal(new Version(10, 0, 26100, 1150), edition1.Version);
        Assert.Equal(4294967296L, edition1.SizeBytes);
    }

    [Fact]
    public void Parse_MountedStatuses()
    {
        var mounts = WinImageDismParse.Mounted(MountedImageInfoOutput);
        Assert.Equal(3, mounts.Count);

        Assert.Equal(@"C:\NitrOS-work-1\mount", mounts[0].MountDir);
        Assert.Equal(MountStatus.Ok, mounts[0].Status);

        Assert.Equal(@"C:\NitrOS-work-2\mount", mounts[1].MountDir);
        Assert.Equal(MountStatus.Invalid, mounts[1].Status);

        Assert.Equal(@"C:\NitrOS-work-3\mount", mounts[2].MountDir);
        Assert.Equal(MountStatus.NeedsRemount, mounts[2].Status);
    }

    [Fact]
    public void Parse_ProvisionedApps()
    {
        var apps = WinImageDismParse.ProvisionedApps(ProvisionedAppsOutput);
        Assert.Equal(2, apps.Count);

        Assert.Equal("Microsoft.XboxApp", apps[0].DisplayName);
        Assert.Equal("Microsoft.XboxApp_1.0.0.0_neutral_~_8wekyb3d8bbwe", apps[0].PackageName);

        Assert.Equal("Microsoft.ZuneVideo", apps[1].DisplayName);
        Assert.Equal("Microsoft.ZuneVideo_2.0.0.0_neutral_~_8wekyb3d8bbwe", apps[1].PackageName);
    }

    [Fact]
    public void Parse_FeatureState()
    {
        var state = WinImageDismParse.FeatureOrCapabilityState(FeatureInfoOutput);
        Assert.Equal("Enabled", state);
    }

    [Fact]
    public void Parse_PackageIdentities()
    {
        var pkgs = WinImageDismParse.PackageIdentities(PackagesOutput);
        Assert.Equal(2, pkgs.Count);
        Assert.Equal("Package_for_RollupFix~31bf3856ad364e35~amd64~~26100.1150.1.0", pkgs[0]);
        Assert.Equal("Package_for_ServicingStack~31bf3856ad364e35~amd64~~26100.1.1.0", pkgs[1]);
    }

    [Fact]
    public void Parse_AccessDeniedDetected()
    {
        Assert.True(WinImageDismParse.IsAccessDenied("Error: 0x80070005 Access is denied."));
        Assert.False(WinImageDismParse.IsAccessDenied("Operation completed successfully."));
    }

    [Fact]
    public void Parse_PendingIsDeferred()
    {
        Assert.True(WinImageDismParse.IsPending("The operation is pending a system reboot."));
        Assert.False(WinImageDismParse.IsPending("Success"));
    }
}
