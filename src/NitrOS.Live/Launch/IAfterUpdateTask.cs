namespace NitrOS.Live.Launch;

public interface IAfterUpdateTask
{
    bool IsRegistered();
    void Register(string exePath, string userSid);
    void Remove();
}
