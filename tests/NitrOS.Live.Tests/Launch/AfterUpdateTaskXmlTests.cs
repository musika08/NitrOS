using System.Xml.Linq;
using NitrOS.Live.Launch;

namespace NitrOS.Live.Tests.Launch;

public sealed class AfterUpdateTaskXmlTests
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    [Fact]
    public void AfterUpdateTaskXml_HasLogonTriggerForUserAndHighestRunLevel()
    {
        string sid = "S-1-5-21-123456789-1000";
        string exe = @"C:\Program Files\NitrOS\NitrOS.exe";
        string xml = AfterUpdateTaskXml.Build(exe, sid);

        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        Assert.NotNull(root);
        Assert.Equal("Task", root.Name.LocalName);
        Assert.Equal("1.2", root.Attribute("version")?.Value);

        var trigger = root.Element(Ns + "Triggers")?.Element(Ns + "LogonTrigger");
        Assert.NotNull(trigger);
        Assert.Equal("true", trigger.Element(Ns + "Enabled")?.Value);
        Assert.Equal(sid, trigger.Element(Ns + "UserId")?.Value);

        var principal = root.Element(Ns + "Principals")?.Element(Ns + "Principal");
        Assert.NotNull(principal);
        Assert.Equal(sid, principal.Element(Ns + "UserId")?.Value);
        Assert.Equal("InteractiveToken", principal.Element(Ns + "LogonType")?.Value);
        Assert.Equal("HighestAvailable", principal.Element(Ns + "RunLevel")?.Value);

        var settings = root.Element(Ns + "Settings");
        Assert.NotNull(settings);
        Assert.Equal("IgnoreNew", settings.Element(Ns + "MultipleInstancesPolicy")?.Value);
        Assert.Equal("false", settings.Element(Ns + "DisallowStartIfOnBatteries")?.Value);
        Assert.Equal("false", settings.Element(Ns + "StopIfGoingOnBatteries")?.Value);
        Assert.Equal("PT10M", settings.Element(Ns + "ExecutionTimeLimit")?.Value);
        Assert.Equal("true", settings.Element(Ns + "Enabled")?.Value);

        var exec = root.Element(Ns + "Actions")?.Element(Ns + "Exec");
        Assert.NotNull(exec);
        Assert.Equal(exe, exec.Element(Ns + "Command")?.Value);
        Assert.Equal("--after-update", exec.Element(Ns + "Arguments")?.Value);
    }

    [Fact]
    public void AfterUpdateTaskXml_EscapesPath()
    {
        string sid = "S-1-5-21-1";
        string exe = @"C:\Apps & Tools\Test <1>.exe";
        string xml = AfterUpdateTaskXml.Build(exe, sid);

        Assert.Contains(@"C:\Apps &amp; Tools\Test &lt;1&gt;.exe", xml);
        var doc = XDocument.Parse(xml);
        var exec = doc.Root?.Element(Ns + "Actions")?.Element(Ns + "Exec");
        Assert.Equal(exe, exec?.Element(Ns + "Command")?.Value);
    }
}
