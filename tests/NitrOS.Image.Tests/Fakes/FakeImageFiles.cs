namespace NitrOS.Image.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Linq;
using NitrOS.Image.Systems;

public sealed class FakeImageFiles : IImageFiles
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Reparse { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Writes { get; } = [];

    public long FreeBytesValue { get; set; } = 50L * 1024 * 1024 * 1024;
    public string FileSystemName { get; set; } = "NTFS";

    public string? FailOn { get; set; }
    public string? CrashOn { get; set; }

    private void Check(string callName)
    {
        if (string.Equals(FailOn, callName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Simulated failure on {callName}");
        }
        if (string.Equals(CrashOn, callName, StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedCrashException($"Simulated crash on {callName}");
        }
    }

    public void CopyTree(string from, string to, Func<string, bool> include)
    {
        Check(nameof(CopyTree));
        var fromPrefix = from.TrimEnd('\\') + "\\";
        var matching = Files.Where(kv => kv.Key.StartsWith(fromPrefix, StringComparison.OrdinalIgnoreCase) && include(kv.Key)).ToList();
        foreach (var kv in matching)
        {
            var rel = kv.Key[fromPrefix.Length..];
            var dest = System.IO.Path.Combine(to, rel);
            Files[dest] = kv.Value;
        }
    }

    public void DeleteUnder(string root, string path)
    {
        Check(nameof(DeleteUnder));
        var normalizedRoot = System.IO.Path.GetFullPath(root).TrimEnd('\\') + "\\";
        var normalizedPath = System.IO.Path.GetFullPath(path);

        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{path}' is not under root '{root}'.");
        }

        Files.Remove(path);
        Reparse.Remove(path);

        var prefix = normalizedPath.TrimEnd('\\') + "\\";
        var subKeys = Files.Keys.Where(k => System.IO.Path.GetFullPath(k).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var k in subKeys)
        {
            Files.Remove(k);
            Reparse.Remove(k);
        }
    }

    public void DeleteWorkTree(string path)
    {
        Check(nameof(DeleteWorkTree));
        var prefix = System.IO.Path.GetFullPath(path).TrimEnd('\\') + "\\";
        var toRemove = Files.Keys.Where(k => System.IO.Path.GetFullPath(k).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var k in toRemove)
        {
            Files.Remove(k);
        }
    }

    public bool Exists(string path)
    {
        Check(nameof(Exists));
        // A drive root or folder prefix exists if any file starts with it
        var prefix = path.TrimEnd('\\') + "\\";
        return Files.ContainsKey(path) || Reparse.Contains(path) || Files.Keys.Any(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public string FileSystem(string folder)
    {
        Check(nameof(FileSystem));
        return FileSystemName;
    }

    public long FreeBytes(string folder)
    {
        Check(nameof(FreeBytes));
        return FreeBytesValue;
    }

    public bool IsReparsePoint(string path)
    {
        Check(nameof(IsReparsePoint));
        return Reparse.Contains(path);
    }

    public void Move(string from, string to)
    {
        Check(nameof(Move));
        if (Files.Remove(from, out var content))
        {
            Files[to] = content;
        }
    }

    public string? ReadText(string path)
    {
        Check(nameof(ReadText));
        return Files.GetValueOrDefault(path);
    }

    public IReadOnlyList<string> SubFolders(string path)
    {
        Check(nameof(SubFolders));
        var prefix = path.TrimEnd('\\') + "\\";
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Files.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rel = key[prefix.Length..];
                var slash = rel.IndexOf('\\');
                if (slash > 0)
                {
                    set.Add(System.IO.Path.Combine(path, rel[..slash]));
                }
            }
        }
        return set.ToList();
    }

    public IReadOnlyList<string> EnumerateFiles(string path, string searchPattern = "*", bool recursive = true)
    {
        Check(nameof(EnumerateFiles));
        var prefix = path.TrimEnd('\\') + "\\";
        var list = new List<string>();
        foreach (var key in Files.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rel = key[prefix.Length..];
                if (!recursive && rel.Contains('\\'))
                {
                    continue;
                }
                list.Add(key);
            }
        }
        return list;
    }

    public void WriteText(string path, string text)
    {
        Check(nameof(WriteText));
        Files[path] = text;
        Writes.Add(path);
    }
}
