using System.Text.Json.Serialization;
using NitrOS.Core.Model;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Journal;

/// <summary>What a target looked like before an action changed it. Serialised with a "kind" discriminator.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(RegistryPrior), "registry")]
[JsonDerivedType(typeof(RegistryTreePrior), "registry-tree")]
[JsonDerivedType(typeof(ServicePrior), "service")]
[JsonDerivedType(typeof(TaskPrior), "task")]
[JsonDerivedType(typeof(StartupPrior), "startup")]
[JsonDerivedType(typeof(PowerPrior), "power")]
[JsonDerivedType(typeof(SchemePrior), "scheme")]
[JsonDerivedType(typeof(LaunchPrior), "launch")]
[JsonDerivedType(typeof(MissingPrior), "missing")]
[JsonDerivedType(typeof(IrreversiblePrior), "irreversible")]
[JsonDerivedType(typeof(FeaturePrior), "feature")]
public abstract record PriorState;

public sealed record RegistryPrior(RegValue? Value) : PriorState;
public sealed record RegistryTreePrior(RegTree? Tree, RegValue? Value) : PriorState;

/// <summary>One service and its start type. A named record is used instead of a tuple so it serialises to JSON.</summary>
public sealed record ServiceStartEntry(string Name, ServiceStartType Start);
public sealed record ServicePrior(IReadOnlyList<ServiceStartEntry> Services) : PriorState;

public sealed record TaskPrior(bool Enabled) : PriorState;

/// <summary>One start-up entry's old data at its location. A named record is used instead of a tuple so it serialises to JSON.</summary>
public sealed record StartupEntry(string Location, string Data);
public sealed record StartupPrior(IReadOnlyList<StartupEntry> Entries) : PriorState;

public sealed record PowerPrior(string Scheme, int? Ac, int? Dc) : PriorState;
public sealed record SchemePrior(string PreviousScheme) : PriorState;
public sealed record LaunchPrior(RegValue? Value) : PriorState;

/// <summary>The target was absent, so the action was skipped.</summary>
public sealed record MissingPrior() : PriorState;

public sealed record IrreversiblePrior(string What, string? Version, string ReinstallHint) : PriorState;
public sealed record FeaturePrior(string? State) : PriorState;
