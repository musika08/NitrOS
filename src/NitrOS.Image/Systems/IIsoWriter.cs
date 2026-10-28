namespace NitrOS.Image.Systems;

using System.Threading;

public interface IIsoWriter
{
    void Write(string mediaRoot, string bootOrderFile, string label, string outputIso, CancellationToken ct);
}
