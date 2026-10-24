namespace NitrOS.Live.Systems;

/// <summary>Package names may carry a star wildcard.</summary>
public interface IAppx
{
    bool IsInstalled(string name);
    bool IsProvisioned(string name);
    void RemoveForAllUsers(string name);
    void Deprovision(string name);
}
