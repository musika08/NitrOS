namespace NitrOS.Live.Systems;

public interface IPowerConfig
{
    string ActiveScheme();
    IReadOnlyList<string> Schemes();
    (int Ac, int Dc)? Get(string scheme, string subgroup, string setting);
    void SetAc(string scheme, string subgroup, string setting, int value);
    void SetDc(string scheme, string subgroup, string setting, int value);
    void SetActive(string scheme);
}
