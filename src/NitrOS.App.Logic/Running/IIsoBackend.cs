namespace NitrOS.App.Logic.Running;

using System;
using System.Collections.Generic;
using System.Threading;
using NitrOS.Image.Building;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;

public interface IIsoBackend
{
    IReadOnlyList<ImageEdition> Editions(string source);
    BuildState? FindResumable(string outputIso);
    BuildState Start(BuildRequest request);
    BuildResult Run(BuildState state, IProgress<BuildProgress> progress, CancellationToken ct);
    void Discard(BuildState state);
    string? HostDismReason();
    PowerState Power();
    string? PathsReason(BuildRequest request);
}
