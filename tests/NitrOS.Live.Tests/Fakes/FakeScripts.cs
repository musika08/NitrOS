using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakeScripts : IScriptRunner
{
    public List<string> Bodies { get; } = [];
    public int ExitCode { get; set; }
    /// <summary>Throws when a script body equals this text.</summary>
    public string? FailOn { get; set; }

    public int Run(string body)
    {
        if (FailOn is not null && string.Equals(FailOn, body, StringComparison.Ordinal))
            throw new InvalidOperationException("FakeScripts failure");
        Bodies.Add(body);
        return ExitCode;
    }
}
