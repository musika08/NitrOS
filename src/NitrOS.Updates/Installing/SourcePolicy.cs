namespace NitrOS.Updates.Installing;

public sealed record SourcePolicy(
    string SourceId,
    IReadOnlyList<string> AllowedHosts,
    IReadOnlyList<string> ExpectedSigners)
{
    public static IReadOnlyList<SourcePolicy> Shipped()
    {
        return
        [
            new SourcePolicy(
                "asus",
                AllowedHosts: ["dlcdnets.asus.com"],
                ExpectedSigners:
                [
                    "ASUSTeK COMPUTER INC.",
                    "ASUSTeK Computer Inc."
                ]),
            new SourcePolicy(
                "gpu-amd",
                AllowedHosts: ["drivers.amd.com"],
                ExpectedSigners:
                [
                    "Advanced Micro Devices, Inc.",
                    "Advanced Micro Devices Inc."
                ]),
            new SourcePolicy(
                "gpu-nvidia",
                AllowedHosts: ["international-gfe.download.nvidia.com", "us.download.nvidia.com"],
                ExpectedSigners:
                [
                    "NVIDIA Corporation"
                ]),
            new SourcePolicy(
                "gpu-intel",
                AllowedHosts: ["downloadmirror.intel.com"],
                ExpectedSigners:
                [
                    "Intel Corporation"
                ])
        ];
    }
}
