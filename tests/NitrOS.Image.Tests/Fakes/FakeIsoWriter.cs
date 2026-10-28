namespace NitrOS.Image.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using NitrOS.Image.Systems;

public sealed class FakeIsoWriter : IIsoWriter
{
    public List<(string MediaRoot, string BootOrderFile, string Label, string OutputIso)> Written { get; } = [];

    public string? FailOn { get; set; }
    public string? CrashOn { get; set; }

    public void Write(string mediaRoot, string bootOrderFile, string label, string outputIso, CancellationToken ct)
    {
        if (string.Equals(FailOn, nameof(Write), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Simulated failure on {nameof(Write)}");
        }
        if (string.Equals(CrashOn, nameof(Write), StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedCrashException($"Simulated crash on {nameof(Write)}");
        }

        Written.Add((mediaRoot, bootOrderFile, label, outputIso));
    }
}
