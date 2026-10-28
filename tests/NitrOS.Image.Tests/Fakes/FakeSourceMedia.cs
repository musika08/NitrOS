namespace NitrOS.Image.Tests.Fakes;

using System;
using NitrOS.Image.Systems;

public sealed class FakeSourceMedia : ISourceMedia
{
    public string FakeRoot { get; set; } = "M:\\";
    public string? FailOn { get; set; }
    public string? CrashOn { get; set; }
    public bool WasOpened { get; private set; }
    public bool WasDisposed { get; private set; }

    public IMountedSource Open(string source)
    {
        if (string.Equals(FailOn, nameof(Open), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Simulated failure on {nameof(Open)}");
        }
        if (string.Equals(CrashOn, nameof(Open), StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedCrashException($"Simulated crash on {nameof(Open)}");
        }

        WasOpened = true;
        return new FakeMountedSource(this, FakeRoot);
    }

    private sealed class FakeMountedSource : IMountedSource
    {
        private readonly FakeSourceMedia _parent;

        public FakeMountedSource(FakeSourceMedia parent, string root)
        {
            _parent = parent;
            Root = root;
        }

        public string Root { get; }

        public void Dispose()
        {
            _parent.WasDisposed = true;
        }
    }
}
