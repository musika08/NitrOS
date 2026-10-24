namespace NitrOS.Live.Systems;

public interface IRegistry
{
    RegValue? GetValue(LiveHive h, string key, string name, RegView v);
    void SetValue(LiveHive h, string key, string name, RegValue value, RegView v);
    void DeleteValue(LiveHive h, string key, string name, RegView v);
    IReadOnlyList<string> GetValueNames(LiveHive h, string key, RegView v);
    RegTree? ExportKey(LiveHive h, string key, RegView v);
    void ImportKey(LiveHive h, RegTree tree, RegView v);
    void DeleteKey(LiveHive h, string key, RegView v);
    bool KeyExists(LiveHive h, string key, RegView v);

    /// <summary>
    /// True when Windows lets this process change the key: it is opened for SetValue and CreateSubKey and closed again, nothing
    /// is written. For a missing key the nearest existing parent is tested (that is where it would be created).
    /// </summary>
    bool CanWrite(LiveHive h, string key, RegView v);
}
