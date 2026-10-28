namespace NitrOS.Image.Building;

using System;

public sealed class BuildRefusedException : InvalidOperationException
{
    public BuildRefusedException(string message) : base(message)
    {
    }
}
