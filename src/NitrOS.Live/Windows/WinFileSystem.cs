using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>
/// Deletes a file or folder after takeown and an icacls grant to BUILTIN\Administrators. Refuses relative paths,
/// wildcards, drive roots, the Windows and Program Files roots, and any tree that contains a junction or symbolic
/// link (takeown /R and icacls /T could otherwise reach outside the target).
/// </summary>
public sealed class WinFileSystem : IFileSystem
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public void TakeOwnershipAndDelete(string path)
    {
        var full = ValidateTarget(path);
        var isDir = Directory.Exists(full);
        if (!isDir && !File.Exists(full)) return;
        RefuseReparsePoints(full, isDir);

        var takeown = isDir ? new[] { "/F", full, "/R", "/D", "Y" } : new[] { "/F", full };
        Run("takeown.exe", takeown);
        var icacls = isDir ? new[] { full, "/grant", "*S-1-5-32-544:F", "/T", "/C" } : new[] { full, "/grant", "*S-1-5-32-544:F", "/C" };
        Run("icacls.exe", icacls);

        if (isDir)
        {
            foreach (var f in Directory.EnumerateFileSystemEntries(full, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false }))
                File.SetAttributes(f, File.GetAttributes(f) & ~(FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden));
            new DirectoryInfo(full).Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden);
            Directory.Delete(full, recursive: true);
        }
        else
        {
            File.SetAttributes(full, FileAttributes.Normal);
            File.Delete(full);
        }
    }

    /// <summary>Returns the full path, or throws for anything that is not a specific, absolute target.</summary>
    public static string ValidateTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0)
            throw new ArgumentException($"Invalid path '{path}'", nameof(path));
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException($"Path '{path}' is not absolute", nameof(path));
        var full = Path.GetFullPath(path).TrimEnd('\\', '/');
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) throw new ArgumentException($"Network or device path '{path}' is not supported", nameof(path));
        var root = Path.GetPathRoot(full)?.TrimEnd('\\', '/') ?? "";
        var protectedRoots = new[]
        {
            root,
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.SystemDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "",
        };
        if (protectedRoots.Any(p => p.Length > 0 && string.Equals(p.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Refusing to delete the protected folder '{full}'", nameof(path));
        return full;
    }

    private static void RefuseReparsePoints(string full, bool isDir)
    {
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"Refusing to delete '{full}': it is a junction or link");
        if (!isDir) return;
        var stack = new Stack<string>();
        stack.Push(full);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var e in new DirectoryInfo(dir).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false }))
            {
                if ((e.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException($"Refusing to delete '{full}': it contains the junction or link '{e.FullName}'");
                if ((e.Attributes & FileAttributes.Directory) != 0) stack.Push(e.FullName);
            }
        }
    }

    private static void Run(string tool, string[] args)
    {
        var (code, stdout, stderr) = ProcessRunner.Run(Path.Combine(Environment.SystemDirectory, tool), args, Timeout);
        if (code != 0) throw new InvalidOperationException($"{tool} failed (exit {code}): {(stderr + " " + stdout).Trim()}");
    }
}
