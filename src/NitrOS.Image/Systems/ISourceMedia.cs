namespace NitrOS.Image.Systems;

using System;

public interface IMountedSource : IDisposable
{
    string Root { get; }
}

public interface ISourceMedia
{
    IMountedSource Open(string source);
}
