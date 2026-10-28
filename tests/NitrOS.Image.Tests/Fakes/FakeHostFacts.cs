namespace NitrOS.Image.Tests.Fakes;

using System;
using NitrOS.Image.Systems;

public sealed class FakeHostFacts : IHostFacts
{
    public Version FakeDismVersion { get; set; } = new Version(10, 0, 26100, 1);
    public string WindowsDir { get; set; } = "C:\\Windows";

    public Version DismVersion() => FakeDismVersion;
}
