namespace NitrOS.Image.Building;

using System.Threading;

public interface IOscdimg
{
    string EnsureVerified(CancellationToken ct);
}
