namespace NitrOS.Image.Systems;

using System;
using System.Collections.Generic;

public interface IImageFiles
{
    bool Exists(string path);
    bool IsReparsePoint(string path);
    void DeleteUnder(string root, string path);
    void CopyTree(string from, string to, Func<string, bool> include);
    void WriteText(string path, string text);
    string? ReadText(string path);
    IReadOnlyList<string> SubFolders(string path);
    IReadOnlyList<string> EnumerateFiles(string path, string searchPattern = "*", bool recursive = true);
    long FreeBytes(string folder);
    string FileSystem(string folder);
    void DeleteWorkTree(string path);
    void Move(string from, string to);
}
