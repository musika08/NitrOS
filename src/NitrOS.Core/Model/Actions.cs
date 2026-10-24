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
public abstract record SwitchAction
{
    [JsonIgnore]
    public bool IsDestructive => this is AppxRemove or CapabilityRemove or PackageRemove or FileRemove;
}

public sealed record RegistrySet(string Hive, string Key, string Name, RegistryKind Kind, string Data) : SwitchAction;
public sealed record ServiceStart(string Service, ServiceStartType Start) : SwitchAction;
public sealed record TaskDisable(string Path) : SwitchAction;
public sealed record AppxRemove(string Name) : SwitchAction;
public sealed record CapabilityRemove(string Name) : SwitchAction;
public sealed record FeatureDisable(string Name) : SwitchAction;
public sealed record PackageRemove(string Pattern) : SwitchAction;
public sealed record FileRemove(string Path) : SwitchAction;
public sealed record PowerSet(string Subgroup, string Setting, int? Ac, int? Dc) : SwitchAction;
public sealed record Script(string Body, string? UndoBody) : SwitchAction;
