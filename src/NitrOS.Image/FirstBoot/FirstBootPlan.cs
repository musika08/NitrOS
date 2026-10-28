namespace NitrOS.Image.FirstBoot;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using NitrOS.Live.Systems;

[JsonDerivedType(typeof(MachineRegSet), "regSet")]
[JsonDerivedType(typeof(MachineRegDelete), "regDelete")]
[JsonDerivedType(typeof(TaskOff), "taskOff")]
[JsonDerivedType(typeof(PowerValue), "powerValue")]
[JsonDerivedType(typeof(ScriptRun), "scriptRun")]
public abstract record MachineItem(string SwitchId);

public sealed record MachineRegSet(string SwitchId, string Key, string Name, RegValue Value) : MachineItem(SwitchId);
public sealed record MachineRegDelete(string SwitchId, string Key, string? Name) : MachineItem(SwitchId);
public sealed record TaskOff(string SwitchId, string Path) : MachineItem(SwitchId);
public sealed record PowerValue(string SwitchId, string Subgroup, string Setting, int? Ac, int? Dc) : MachineItem(SwitchId);
public sealed record ScriptRun(string SwitchId, string Body) : MachineItem(SwitchId);

[JsonDerivedType(typeof(UserRegSet), "regSet")]
[JsonDerivedType(typeof(UserRegDelete), "regDelete")]
public abstract record UserItem(string SwitchId);

public sealed record UserRegSet(string SwitchId, string Key, string Name, RegValue Value) : UserItem(SwitchId);
public sealed record UserRegDelete(string SwitchId, string Key, string? Name) : UserItem(SwitchId);

public sealed class FirstBootPlan
{
    private readonly List<MachineItem> _machine = [];
    private readonly List<UserItem> _user = [];

    public IReadOnlyList<MachineItem> Machine => _machine;
    public IReadOnlyList<UserItem> User => _user;

    public void AddMachine(MachineItem item) => _machine.Add(item);
    public void AddUser(UserItem item) => _user.Add(item);
}
