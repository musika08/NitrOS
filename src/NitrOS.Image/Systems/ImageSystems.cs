namespace NitrOS.Image.Systems;

using NitrOS.Live.Systems;

public sealed record ImageSystems(
    IImageServicing Dism,
    IOfflineRegistry Registry,
    IImageFiles Files,
    ISourceMedia Media,
    IIsoWriter Iso,
    IHostFacts Host,
    IPowerSource Power);
