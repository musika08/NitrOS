namespace NitrOS.Image.Systems;

using System;

public interface IHostFacts
{
    Version DismVersion();
    string WindowsDir { get; }
}
