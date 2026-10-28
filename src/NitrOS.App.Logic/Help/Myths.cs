namespace NitrOS.App.Logic.Help;

public sealed record Myth(string Name, string Verdict, string Why);

public static class Myths
{
    public static IReadOnlyList<Myth> All { get; } =
    [
        new Myth(
            "SysMain off",
            "No effect",
            "No measured gaming gain on SSDs and SysMain uses free memory that is released on demand."),
        new Myth(
            "memory compression off",
            "Harmful",
            "Compression trades minimal CPU to keep pages in RAM instead of paging to disk, which is vital on shared-memory handhelds."),
        new Myth(
            "pagefile off",
            "Harmful",
            "Removing the pagefile lowers the system commit limit that games can allocate and breaks crash dumps."),
        new Myth(
            "timer resolution",
            "No effect",
            "Timer resolution requests have been per-process since Windows 10 and offer no global benefit on Windows 11."),
        new Myth(
            "Nagle and TcpAckFrequency",
            "No effect",
            "Modern online games use UDP so TCP acknowledgment tweaks have no effect on gaming latency."),
        new Myth(
            "NetworkThrottlingIndex and SystemResponsiveness",
            "No effect",
            "These multimedia playback settings do not govern game network performance."),
        new Myth(
            "Win32PrioritySeparation",
            "No effect",
            "The default Windows value already boosts foreground application scheduling priority."),
        new Myth(
            "MSI mode",
            "No effect",
            "Modern GPU and storage drivers already use message signaled interrupts by default."),
        new Myth(
            "MPO off",
            "No effect",
            "Disabling multiplane overlays is a display glitch troubleshooting workaround that yields no gaming gain."),
        new Myth(
            "core parking",
            "No effect",
            "Modern CPU firmware and Windows power management manage core parking without manual tweaks."),
        new Myth(
            "HPET and bcdedit timer tweaks",
            "Harmful",
            "Disabling dynamic ticks or forcing platform clock degrades performance in controlled benchmarks."),
        new Myth(
            "Spectre and Meltdown mitigations off",
            "Harmful",
            "Disabling CPU mitigations creates severe security vulnerabilities for negligible gaming gain."),
        new Myth(
            "Office Click-to-Run off",
            "Harmful",
            "Stopping the Click-to-Run service prevents Office from receiving essential security updates."),
        new Myth(
            "touch keyboard service off or TextInputHost rename",
            "Harmful",
            "Disabling the service or renaming its executable breaks the on-screen keyboard required on handhelds."),
        new Myth(
            "Error Reporting off",
            "Harmful",
            "Windows Error Reporting runs on demand and Microsoft guidance advises keeping it enabled.")
    ];
}
