namespace NitrOS.Image.Tests.Fakes;

using System;
using System.Threading;
using NitrOS.Image.Building;

public sealed class FakeOscdimg : IOscdimg
{
    public string Path { get; set; } = "C:\\tools\\oscdimg.exe";
    public string EnsureVerified(CancellationToken ct) => Path;
}
