namespace NitrOS.Image.Systems;

using System;
using System.Collections.Generic;
using NitrOS.Live.Systems;

public enum WriteCheck
{
    Writable,
    Refused
}

public interface IOfflineHive : IDisposable
{
    string MountName { get; }
    RegValue? GetValue(string key, string name);
    IReadOnlyList<string> ValueNames(string key);
    bool KeyExists(string key);
    WriteCheck CanWrite(string key);
    void SetValue(string key, string name, RegValue value);
    bool DeleteValue(string key, string name);
    bool DeleteKey(string key);
}

public interface IOfflineRegistry
{
    IOfflineHive Load(string hiveFile, string mountName);
    IReadOnlyList<string> LoadedNitrOSHives();
    void Unload(string mountName);
}
