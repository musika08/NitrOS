namespace NitrOS.Image.Tests.Fakes;

using System;

public sealed class SimulatedCrashException : Exception
{
    public SimulatedCrashException(string message) : base(message)
    {
    }
}
