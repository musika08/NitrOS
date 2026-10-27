using Microsoft.Win32;

namespace NitrOS.Measure.Guest;

public interface IKvpWriter
{
    void Set(string key, string value);
}

public sealed class WinKvpWriter : IKvpWriter
{
    public void Set(string key, string value)
    {
        using var regKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Virtual Machine\Guest");
        regKey.SetValue(key, value, RegistryValueKind.String);
    }
}
