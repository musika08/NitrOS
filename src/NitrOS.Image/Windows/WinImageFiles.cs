namespace NitrOS.Image.Windows;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using NitrOS.Image.Building;
using NitrOS.Image.Offline;
using NitrOS.Image.Systems;

public sealed class WinImageFiles : IImageFiles
{
    public bool Exists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    public bool IsReparsePoint(string path)
    {
        if (!Exists(path)) return false;
        var attr = File.GetAttributes(path);
        return (attr & FileAttributes.ReparsePoint) != 0;
    }

    public void DeleteUnder(string root, string path)
    {
        if (!ImagePaths.IsUnder(root, path))
        {
            throw new ArgumentException($"Path '{path}' is not under root '{root}'.");
        }

        if (!Exists(path)) return;

        Privileges.Enable("SeBackupPrivilege", "SeRestorePrivilege");

        DeleteInternal(path);
    }

    private void DeleteInternal(string path)
    {
        var attr = File.GetAttributes(path);
        if ((attr & FileAttributes.ReparsePoint) != 0)
        {
            DeleteByHandle(path, isDirectory: (attr & FileAttributes.Directory) != 0);
            return;
        }

        if ((attr & FileAttributes.Directory) != 0)
        {
            foreach (var subDir in Directory.GetDirectories(path))
            {
                DeleteInternal(subDir);
            }
            foreach (var file in Directory.GetFiles(path))
            {
                DeleteByHandle(file, isDirectory: false);
            }
            DeleteByHandle(path, isDirectory: true);
        }
        else
        {
            DeleteByHandle(path, isDirectory: false);
        }
    }

    private static void DeleteByHandle(string path, bool isDirectory)
    {
        var flags = ImageNativeMethods.FILE_FLAG_BACKUP_SEMANTICS | ImageNativeMethods.FILE_FLAG_OPEN_REPARSE_POINT;
        using var handle = ImageNativeMethods.CreateFileW(
            path,
            ImageNativeMethods.DELETE | ImageNativeMethods.SYNCHRONIZE,
            ImageNativeMethods.FILE_SHARE_READ | ImageNativeMethods.FILE_SHARE_WRITE | ImageNativeMethods.FILE_SHARE_DELETE,
            IntPtr.Zero,
            ImageNativeMethods.OPEN_EXISTING,
            flags,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            // If failed to open, try resetting attributes and standard delete
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                if (isDirectory) Directory.Delete(path);
                else File.Delete(path);
                return;
            }
            catch
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open '{path}' for deletion.");
            }
        }

        var info = new ImageNativeMethods.FILE_DISPOSITION_INFO_EX
        {
            Flags = ImageNativeMethods.FILE_DISPOSITION_FLAG_DELETE |
                    ImageNativeMethods.FILE_DISPOSITION_FLAG_POSIX_SEMANTICS |
                    ImageNativeMethods.FILE_DISPOSITION_FLAG_IGNORE_READONLY_ATTRIBUTE
        };

        var size = (uint)Marshal.SizeOf<ImageNativeMethods.FILE_DISPOSITION_INFO_EX>();
        if (!ImageNativeMethods.SetFileInformationByHandle(handle, ImageNativeMethods.FileDispositionInfoEx, ref info, size))
        {
            // Fall back to standard delete if POSIX/Ex is not supported
            handle.Close();
            File.SetAttributes(path, FileAttributes.Normal);
            if (isDirectory) Directory.Delete(path);
            else File.Delete(path);
        }
    }

    public void CopyTree(string from, string to, Func<string, bool> include)
    {
        var fromDir = new DirectoryInfo(from);
        if (!fromDir.Exists) return;

        Directory.CreateDirectory(to);

        foreach (var file in fromDir.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            if (include(file.FullName))
            {
                var rel = Path.GetRelativePath(from, file.FullName);
                var dest = Path.Combine(to, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                file.CopyTo(dest, overwrite: true);
            }
        }
    }

    public void WriteText(string path, string text)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, text);
    }

    public string? ReadText(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public IReadOnlyList<string> SubFolders(string path)
    {
        return Directory.Exists(path) ? Directory.GetDirectories(path) : [];
    }

    public IReadOnlyList<string> EnumerateFiles(string path, string searchPattern = "*", bool recursive = true)
    {
        if (!Directory.Exists(path)) return [];
        var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(path, searchPattern, opt);
    }

    public long FreeBytes(string folder)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(folder));
        if (root == null) return 0;
        var drive = new DriveInfo(root);
        return drive.AvailableFreeSpace;
    }

    public string FileSystem(string folder)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(folder));
        if (root == null) return "";
        var drive = new DriveInfo(root);
        return drive.DriveFormat;
    }

    public void DeleteWorkTree(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public void Move(string from, string to)
    {
        var dir = Path.GetDirectoryName(to);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.Move(from, to, overwrite: true);
    }
}
