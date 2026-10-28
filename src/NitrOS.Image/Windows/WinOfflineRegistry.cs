namespace NitrOS.Image.Windows;

using System;
using System.Collections.Generic;
using Microsoft.Win32;
using NitrOS.Core.Model;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;

public sealed class WinOfflineRegistry : IOfflineRegistry
{
    private sealed class WinOfflineHive : IOfflineHive
    {
        private readonly WinOfflineRegistry _parent;
        private readonly RegistryKey _rootKey;
        private bool _disposed;

        public string MountName { get; }

        public WinOfflineHive(WinOfflineRegistry parent, string mountName, RegistryKey rootKey)
        {
            _parent = parent;
            MountName = mountName;
            _rootKey = rootKey;
        }

        private RegistryKey? OpenSub(string key, bool writable)
        {
            if (string.IsNullOrWhiteSpace(key)) return _rootKey;
            return writable ? _rootKey.CreateSubKey(key) : _rootKey.OpenSubKey(key);
        }

        public RegValue? GetValue(string key, string name)
        {
            using var k = OpenSub(key, writable: false);
            if (k == null) return null;

            var val = k.GetValue(name);
            if (val == null) return null;

            var kind = k.GetValueKind(name);
            return kind switch
            {
                RegistryValueKind.DWord => new RegValue(RegistryKind.Dword, ((int)val).ToString()),
                RegistryValueKind.QWord => new RegValue(RegistryKind.Qword, ((long)val).ToString()),
                RegistryValueKind.String => new RegValue(RegistryKind.String, (string)val),
                RegistryValueKind.ExpandString => new RegValue(RegistryKind.ExpandString, (string)val),
                RegistryValueKind.MultiString => new RegValue(RegistryKind.MultiString, string.Join("\n", (string[])val)),
                RegistryValueKind.Binary => new RegValue(RegistryKind.Binary, Convert.ToHexStringLower((byte[])val)),
                _ => new RegValue(RegistryKind.String, val.ToString() ?? "")
            };
        }

        public IReadOnlyList<string> ValueNames(string key)
        {
            using var k = OpenSub(key, writable: false);
            return k?.GetValueNames() ?? [];
        }

        public bool KeyExists(string key)
        {
            using var k = OpenSub(key, writable: false);
            return k != null;
        }

        public WriteCheck CanWrite(string key)
        {
            try
            {
                using var k = OpenSub(key, writable: true);
                return k != null ? WriteCheck.Writable : WriteCheck.Refused;
            }
            catch
            {
                return WriteCheck.Refused;
            }
        }

        public void SetValue(string key, string name, RegValue value)
        {
            using var k = OpenSub(key, writable: true)
                ?? throw new InvalidOperationException($"Could not open key '{key}' under hive '{MountName}' for write.");

            switch (value.Kind)
            {
                case RegistryKind.Dword:
                    k.SetValue(name, int.Parse(value.Data), RegistryValueKind.DWord);
                    break;
                case RegistryKind.Qword:
                    k.SetValue(name, long.Parse(value.Data), RegistryValueKind.QWord);
                    break;
                case RegistryKind.String:
                    k.SetValue(name, value.Data, RegistryValueKind.String);
                    break;
                case RegistryKind.ExpandString:
                    k.SetValue(name, value.Data, RegistryValueKind.ExpandString);
                    break;
                case RegistryKind.MultiString:
                    k.SetValue(name, value.Data.Split('\n'), RegistryValueKind.MultiString);
                    break;
                case RegistryKind.Binary:
                    k.SetValue(name, Convert.FromHexString(value.Data), RegistryValueKind.Binary);
                    break;
                default:
                    k.SetValue(name, value.Data);
                    break;
            }
        }

        public bool DeleteValue(string key, string name)
        {
            using var k = OpenSub(key, writable: true);
            if (k == null) return false;
            try
            {
                k.DeleteValue(name, throwOnMissingValue: false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteKey(string key)
        {
            try
            {
                _rootKey.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _rootKey.Dispose();
                _parent.Unload(MountName);
            }
        }
    }

    public IOfflineHive Load(string hiveFile, string mountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hiveFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountName);

        Privileges.Enable("SeBackupPrivilege", "SeRestorePrivilege");

        var ret = ImageNativeMethods.RegLoadKeyW(ImageNativeMethods.HKEY_LOCAL_MACHINE, mountName, hiveFile);
        if (ret != ImageNativeMethods.ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"RegLoadKey failed with error {ret} for file '{hiveFile}' and mount '{mountName}'.");
        }

        var key = Registry.LocalMachine.OpenSubKey(mountName, writable: true)
            ?? throw new InvalidOperationException($"Could not open loaded offline hive under HKLM\\{mountName}");

        return new WinOfflineHive(this, mountName, key);
    }

    public void Unload(string mountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mountName);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        Privileges.Enable("SeBackupPrivilege", "SeRestorePrivilege");

        var ret = ImageNativeMethods.RegUnLoadKeyW(ImageNativeMethods.HKEY_LOCAL_MACHINE, mountName);
        if (ret != ImageNativeMethods.ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"RegUnLoadKey failed with error {ret} for mount '{mountName}'.");
        }
    }

    public IReadOnlyList<string> LoadedNitrOSHives()
    {
        var list = new List<string>();
        using var hklm = Registry.LocalMachine;
        foreach (var subName in hklm.GetSubKeyNames())
        {
            if (subName.StartsWith("NitrOS.Image.", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(subName);
            }
        }
        return list;
    }
}
