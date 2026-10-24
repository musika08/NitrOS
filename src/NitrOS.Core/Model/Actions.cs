using System.Text.Json.Serialization;

namespace NitrOS.Core.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RegistrySet), "registry.set")]
[JsonDerivedType(typeof(ServiceStart), "service.start")]
[JsonDerivedType(typeof(TaskDisable), "task.disable")]
[JsonDerivedType(typeof(AppxRemove), "appx.remove")]
[JsonDerivedType(typeof(CapabilityRemove), "capability.remove")]
[JsonDerivedType(typeof(FeatureDisable), "feature.disable")]
[JsonDerivedType(typeof(PackageRemove), "package.remove")]
[JsonDerivedType(typeof(FileRemove), "file.remove")]
[JsonDerivedType(typeof(PowerSet), "power.set")]
[JsonDerivedType(typeof(Script), "script")]
[JsonDerivedType(typeof(ProgramUninstall), "program.uninstall")]
[JsonDerivedType(typeof(StartupDisable), "startup.disable")]
[JsonDerivedType(typeof(RegistryDelete), "registry.delete")]
[JsonDerivedType(typeof(PowerScheme), "power.scheme")]
[JsonDerivedType(typeof(LaunchAtLogon), "launch.at-logon")]
public abstract record SwitchAction
{
    [JsonIgnore]
    public bool IsDestructive => this is AppxRemove or CapabilityRemove or PackageRemove or FileRemove or ProgramUninstall;
}

/// <summary>
/// Sets one registry value. An empty <c>Name</c> targets the key's default value.
/// For <see cref="RegistryKind.MultiString"/>, <c>Data</c> holds the strings separated by newline (LF, U+000A) characters.
/// </summary>
public sealed record RegistrySet(string Hive, string Key, string Name, RegistryKind Kind, string Data) : SwitchAction;
public sealed record ServiceStart(string Service, ServiceStartType Start) : SwitchAction;
public sealed record TaskDisable(string Path) : SwitchAction;
public sealed record AppxRemove(string Name) : SwitchAction;
public sealed record CapabilityRemove(string Name) : SwitchAction;
public sealed record FeatureDisable(string Name) : SwitchAction;
public sealed record PackageRemove(string Pattern) : SwitchAction;
public sealed record FileRemove(string Path) : SwitchAction;
public sealed record PowerSet(string Subgroup, string Setting, int? Ac = null, int? Dc = null) : SwitchAction;
public sealed record Script(string Body, string? UndoBody = null) : SwitchAction;

/// <summary>Uninstalls an installed program found by publisher and display name. <c>DisplayName</c> may contain <c>*</c> wildcards.</summary>
public sealed record ProgramUninstall(string DisplayName, string Publisher, string? Args = null) : SwitchAction;

/// <summary>Turns off one start-up entry. <c>Hive</c> (HKLM or HKCU) is required for <see cref="StartupSource.Run"/> and must be null otherwise.</summary>
public sealed record StartupDisable(StartupSource Source, string Name, string? Hive = null) : SwitchAction;

/// <summary>Deletes a registry value, or the whole key when <c>Name</c> is null. Not destructive because the PC tuner exports the key first.</summary>
public sealed record RegistryDelete(string Hive, string Key, string? Name = null) : SwitchAction;

/// <summary>Makes the power plan with this GUID the active one.</summary>
public sealed record PowerScheme(string Scheme) : SwitchAction;

/// <summary>Starts a program when the user signs in.</summary>
public sealed record LaunchAtLogon(string Program, string? Args = null) : SwitchAction;
