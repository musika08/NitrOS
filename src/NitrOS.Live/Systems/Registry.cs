using NitrOS.Core.Model;

namespace NitrOS.Live.Systems;

public enum LiveHive { LocalMachine, CurrentUser }

public enum RegView { Default, Registry32 }

/// <summary>A registry value. Data uses the catalog text format; multi-string is newline-separated.</summary>
public sealed record RegValue(RegistryKind Kind, string Data);

/// <summary>A named value inside a RegTree. A named record is used instead of a tuple so it serialises to JSON.</summary>
public sealed record RegNamedValue(string Name, RegValue Value);

public sealed record RegTree(string Key, IReadOnlyList<RegNamedValue> Values, IReadOnlyList<RegTree> SubKeys);
