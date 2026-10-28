using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>A parsed uninstall command: the program to start and its separate arguments. ProductCode is set for msiexec.</summary>
public sealed record UninstallCommand(string Executable, IReadOnlyList<string> Arguments, bool IsMsiExec, string? ProductCode = null);

/// <summary>The uninstaller may not be run from where it is; the message is the user-facing detail.</summary>
public sealed class UninstallRefusedException(string message) : InvalidOperationException(message);

/// <summary>
/// Reads the Uninstall keys (HKLM 64-bit, HKLM 32-bit, HKCU; both registry views), checks the uninstaller's
/// Authenticode signer against the publisher, and runs the uninstaller with a separate argument list.
/// </summary>
public sealed class WinUninstaller : IUninstaller
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(30);
    private static readonly Regex UnquotedExeRx = new(@"^(.+?\.exe)(?=\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MsiOperationRx = new(@"^[/-]([xi])(\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\})?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GuidArgRx = new(@"^\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$", RegexOptions.Compiled);

    /// <summary>The only msiexec options passed through, besides the single /X{GUID}.</summary>
    private static readonly HashSet<string> MsiAllowedOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "/qn", "/quiet", "/passive", "/qb", "/qb!", "/qb-", "/norestart", "REBOOT=ReallySuppress",
    };

    public const string UserWritableDetail = "per-user program in a user-writable folder; remove it from Settings > Apps";

    private static string MsiExecPath => Path.Combine(Environment.SystemDirectory, "msiexec.exe");

    public IReadOnlyList<UninstallEntry> Find(string displayNamePattern, string publisher)
    {
        var result = new List<UninstallEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, view, label) in Roots())
        {
            using var b = RegistryKey.OpenBaseKey(hive, view);
            using var root = b.OpenSubKey(UninstallKey);
            if (root is null) continue;
            foreach (var sub in root.GetSubKeyNames())
            {
                var keyPath = label + "\\" + sub;
                if (!seen.Add(keyPath)) continue;
                using var k = root.OpenSubKey(sub);
                if (k?.GetValue("DisplayName") is not string name || string.IsNullOrWhiteSpace(name)) continue;
                var pub = k.GetValue("Publisher") as string ?? "";
                if (!Glob.IsMatch(displayNamePattern, name.Trim())) continue;
                if (!string.Equals(pub.Trim(), publisher.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new UninstallEntry(keyPath, name.Trim(), pub.Trim(), k.GetValue("DisplayVersion") as string,
                    k.GetValue("QuietUninstallString") as string, k.GetValue("UninstallString") as string));
            }
        }
        return result;
    }

    /// <summary>
    /// Only the machine-wide Uninstall keys (both views). HKCU entries are ignored: any standard user can write them, so a
    /// planted per-user entry could point a trusted signed tool, or msiexec /X of another product, at its own arguments.
    /// </summary>
    internal static IReadOnlyList<(RegistryHive Hive, RegistryView View, string Label)> Roots() =>
    [
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"HKLM\" + UninstallKey),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    /// <summary>
    /// False when the command cannot be parsed, the program is missing, or a signer does not match. Throws
    /// <see cref="UninstallRefusedException"/> when the program sits in the Windows folder or in a user-writable place.
    /// </summary>
    public bool SignerMatches(UninstallEntry entry, string publisher)
    {
        UninstallCommand cmd;
        try { cmd = Parse(Compose(entry, null)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException) { return false; }
        using var files = VerifiedFiles.Open(cmd);
        return files is not null && files.Matches(entry, publisher);
    }

    /// <summary>
    /// Verifies again through handles opened without write or delete sharing, so the files cannot be swapped between the
    /// check and the start; the handles are released once the process has started.
    /// </summary>
    public int Run(UninstallEntry entry, string? args)
    {
        var cmd = CommandFor(entry, args);
        using var files = VerifiedFiles.Open(cmd) ?? throw new FileNotFoundException($"Uninstaller for '{entry.DisplayName}' not found", cmd.Executable);
        if (!files.Matches(entry, entry.Publisher))
            throw new UninstallRefusedException($"{entry.DisplayName}: signature check failed");
        var (code, _, _) = ProcessRunner.Run(cmd.Executable, cmd.Arguments, RunTimeout, files.Dispose);
        return code;
    }

    /// <summary>The uninstaller (and for msiexec the product's cached .msi), open with FileShare.Read only.</summary>
    private sealed class VerifiedFiles : IDisposable
    {
        private readonly string _exe;
        private SafeFileHandle? _exeHandle;
        private readonly string? _package;
        private SafeFileHandle? _packageHandle;
        private readonly bool _msi;

        private VerifiedFiles(string exe, SafeFileHandle exeHandle, string? package, SafeFileHandle? packageHandle, bool msi)
        {
            _exe = exe; _exeHandle = exeHandle; _package = package; _packageHandle = packageHandle; _msi = msi;
        }

        /// <summary>Null when the program is not there. An unknown MSI product gives files that never match.</summary>
        public static VerifiedFiles? Open(UninstallCommand cmd)
        {
            CheckLocation(cmd.Executable, cmd.IsMsiExec);
            if (!File.Exists(cmd.Executable)) return null;
            var exeHandle = OpenLocked(cmd.Executable);
            try
            {
                RequireAt(exeHandle, cmd.Executable);
                if (!cmd.IsMsiExec)
                {
                    if (IsUserWritable(cmd.Executable)) throw new UninstallRefusedException(UserWritableDetail);
                    return new VerifiedFiles(cmd.Executable, exeHandle, null, null, false);
                }
                var package = LocalPackage(cmd.ProductCode!);
                if (package is null || !Path.IsPathFullyQualified(package) || SecureFs.IsNetworkOrDevice(package) || !File.Exists(package))
                    return new VerifiedFiles(cmd.Executable, exeHandle, null, null, true);
                var packageHandle = OpenLocked(package);
                try
                {
                    RequireAt(packageHandle, package);
                    if (IsUserWritable(package)) throw new UninstallRefusedException(UserWritableDetail);
                }
                catch { packageHandle.Dispose(); throw; }
                return new VerifiedFiles(cmd.Executable, exeHandle, package, packageHandle, true);
            }
            catch { exeHandle.Dispose(); throw; }
        }

        public bool Matches(UninstallEntry entry, string publisher)
        {
            if (_exeHandle is null) throw new ObjectDisposedException(nameof(VerifiedFiles));
            if (!_msi) return TrustedSignerNames(_exeHandle, _exe) is { } names && names.Any(n => NamesMatch(n, publisher));

            // msiexec.exe must be Microsoft's, and the product's own cached .msi must be signed by the publisher.
            if (_packageHandle is null || _package is null) return false;
            var msiexec = TrustedSignerNames(_exeHandle, _exe);
            if (msiexec is null || !msiexec.Any(n => NamesMatch(n, "Microsoft Corporation"))) return false;
            var signers = TrustedSignerNames(_packageHandle, _package);
            return signers is not null && signers.Any(n => NamesMatch(n, publisher)) && NamesMatch(entry.Publisher, publisher);
        }

        public void Dispose()
        {
            _packageHandle?.Dispose();
            _packageHandle = null;
            _exeHandle?.Dispose();
            _exeHandle = null;
        }
    }

    /// <summary>Read access, sharing read only: nobody can write, rename or delete the file while the handle is open.</summary>
    internal static SafeFileHandle OpenLocked(string path) => Authenticode.OpenLocked(path);

    private static bool IsNetworkOrDevice(string path) => SecureFs.IsNetworkOrDevice(path);

    /// <summary>The pinned handle must be the expected file itself, not something a junction, link or 8.3 alias leads to.</summary>
    internal static void RequireAt(SafeFileHandle handle, string expected)
    {
        if (!SecureFs.IsAt(handle, expected))
            throw new UninstallRefusedException($"uninstaller path resolves somewhere else: {expected} -> {SecureFs.FinalPath(handle)}");
    }

    /// <summary>Refuses UNC and device paths, and any program inside %SystemRoot% other than System32/SysWOW64 msiexec.exe.</summary>
    internal static void CheckLocation(string exe, bool isMsiExec)
    {
        if (IsNetworkOrDevice(exe)) throw new UninstallRefusedException($"uninstaller on a network or device path is refused: {exe}");
        if (isMsiExec) return;
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + @"\";
        if (Path.GetFullPath(exe).StartsWith(windows, StringComparison.OrdinalIgnoreCase))
            throw new UninstallRefusedException($"uninstaller inside the Windows folder is refused: {exe}");
    }

    /// <summary>The product's cached package (MsiGetProductInfoW "LocalPackage"); null for an unknown product or property.</summary>
    internal static string? LocalPackage(string productCode)
    {
        uint len = 0;
        var rc = NativeMethods.MsiGetProductInfoW(productCode, "LocalPackage", null, ref len);
        if (rc != NativeMethods.ERROR_SUCCESS && rc != NativeMethods.ERROR_MORE_DATA) return null;
        if (len == 0) return null;
        var buf = new char[len + 1];
        len = (uint)buf.Length;
        rc = NativeMethods.MsiGetProductInfoW(productCode, "LocalPackage", buf, ref len);
        if (rc != NativeMethods.ERROR_SUCCESS || len == 0) return null;
        return new string(buf, 0, (int)len);
    }

    // ---- user-writable check (allow-list) ----

    /// <summary>Rights that let someone replace the file itself.</summary>
    public const int FileWriteMask = SecureFs.WriteData | SecureFs.AppendData | SecureFs.DeleteRight | SecureFs.WriteDac
        | SecureFs.WriteOwner | SecureFs.GenericAll | SecureFs.GenericWrite;
    /// <summary>Rights on the program's own folder that allow swapping it or planting a DLL next to it.</summary>
    public const int FolderWriteMask = SecureFs.WriteData | SecureFs.DeleteChild | SecureFs.DeleteRight | SecureFs.WriteDac
        | SecureFs.WriteOwner | SecureFs.GenericAll | SecureFs.GenericWrite;
    /// <summary>
    /// Rights on a folder further up that allow renaming the path away or taking the folder over. Creating new
    /// subfolders (AppendData, which Authenticated Users have on C:\ itself) cannot change an existing path, so it is not counted.
    /// </summary>
    public const int AncestorWriteMask = SecureFs.DeleteChild | SecureFs.DeleteRight | SecureFs.WriteDac | SecureFs.WriteOwner
        | SecureFs.GenericAll | SecureFs.GenericWrite;

    /// <summary>
    /// True when any SID other than Administrators, SYSTEM and TrustedInstaller owns the file, its folder or any folder up
    /// to the volume root, or has a write, delete, change-permission or take-ownership right on one of them.
    /// </summary>
    internal static bool IsUserWritable(string file)
    {
        var trusted = SecureFs.TrustedSids();
        const AccessControlSections sections = AccessControlSections.Access | AccessControlSections.Owner;
        if (OthersCanWrite(new FileInfo(file).GetAccessControl(sections), trusted, FileWriteMask)) return true;
        var dir = new FileInfo(file).Directory;
        var mask = FolderWriteMask;
        while (dir is not null)
        {
            if (OthersCanWrite(dir.GetAccessControl(sections), trusted, mask)) return true;
            mask = AncestorWriteMask;
            dir = dir.Parent;
        }
        return false;
    }

    /// <summary>
    /// Allow-list: true when the owner is not trusted, or any allow entry (not inherit-only) for an untrusted SID has one
    /// of the rights. Deny entries are not subtracted.
    /// </summary>
    public static bool OthersCanWrite(FileSystemSecurity security, IReadOnlySet<string> trusted, int mask) =>
        SecureFs.OthersCanWrite(security, trusted, mask, includeInheritOnly: false);

    /// <summary>msiexec user-interface levels; with no action args they are replaced by /qn.</summary>
    private static readonly HashSet<string> MsiUiOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "/qn", "/quiet", "/passive", "/qb", "/qb!", "/qb-", "/norestart",
    };

    /// <summary>
    /// The command <see cref="Run"/> starts for this entry and these args. An msiexec uninstall given no args is always quiet:
    /// its own UI options are replaced by /qn /norestart, so no msiexec window waits for a click.
    /// </summary>
    public static UninstallCommand CommandFor(UninstallEntry entry, string? args)
    {
        var cmd = Parse(Compose(entry, args));
        if (!cmd.IsMsiExec || !string.IsNullOrWhiteSpace(args)) return cmd;
        var kept = cmd.Arguments.Where(a => !MsiUiOptions.Contains(a));
        return cmd with { Arguments = kept.Concat(["/qn", "/norestart"]).ToList() };
    }

    /// <summary>QuietUninstallString, else UninstallString, with the extra args appended after a space.</summary>
    public static string Compose(UninstallEntry entry, string? args)
    {
        var baseCmd = !string.IsNullOrWhiteSpace(entry.QuietUninstallString) ? entry.QuietUninstallString
            : !string.IsNullOrWhiteSpace(entry.UninstallString) ? entry.UninstallString
            : throw new ArgumentException($"'{entry.DisplayName}' has no uninstall command", nameof(entry));
        baseCmd = baseCmd.Trim();
        return string.IsNullOrWhiteSpace(args) ? baseCmd : baseCmd + " " + args.Trim();
    }

    /// <summary>
    /// Splits an uninstall command into program and arguments. Accepts a quoted path, an unquoted path ending in .exe
    /// (spaces allowed), and MsiExec.exe /X{GUID}. Relative programs, non-.exe programs and msiexec operations other
    /// than uninstall throw instead of being guessed.
    /// </summary>
    public static UninstallCommand Parse(string command)
    {
        var text = command?.Trim() ?? "";
        if (text.Length == 0) throw new ArgumentException("Empty uninstall command", nameof(command));

        string exe, rest;
        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            if (close < 0) throw new ArgumentException($"Unterminated quote in '{command}'", nameof(command));
            exe = text[1..close];
            rest = text[(close + 1)..];
            if (rest.Length > 0 && !char.IsWhiteSpace(rest[0])) throw new NotSupportedException($"Unsupported uninstall command '{command}'");
        }
        else if (text.Equals("msiexec", StringComparison.OrdinalIgnoreCase)
            || (text.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase) && char.IsWhiteSpace(text[7])))
        {
            exe = "msiexec.exe";
            rest = text[7..];
        }
        else
        {
            var m = UnquotedExeRx.Match(text);
            if (!m.Success) throw new NotSupportedException($"Unsupported uninstall command '{command}'");
            exe = m.Groups[1].Value;
            rest = text[m.Length..];
        }
        exe = Environment.ExpandEnvironmentVariables(exe.Trim());
        var args = SplitArguments(rest);

        if (IsMsiExecProgram(exe)) return ParseMsi(args, command!);

        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Uninstaller '{exe}' is not an .exe");
        if (!Path.IsPathFullyQualified(exe))
            throw new NotSupportedException($"Uninstaller '{exe}' is not a full path");
        if (IsNetworkOrDevice(exe))
            throw new NotSupportedException($"Uninstaller '{exe}' is on a network or device path");
        return new UninstallCommand(Path.GetFullPath(exe), args, false);
    }

    /// <summary>msiexec counts only when it has no folder or sits in System32 / SysWOW64; any other msiexec.exe is an ordinary program.</summary>
    private static bool IsMsiExecProgram(string exe)
    {
        var file = Path.GetFileName(exe);
        if (!file.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) && !file.Equals("msiexec", StringComparison.OrdinalIgnoreCase)) return false;
        var dir = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(dir)) return true;
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return Path.IsPathFullyQualified(exe) && (
            string.Equals(Path.GetFullPath(dir).TrimEnd('\\'), Path.Combine(windows, "System32"), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFullPath(dir).TrimEnd('\\'), Path.Combine(windows, "SysWOW64"), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Allow-list: exactly one /X{GUID} (or /X {GUID}; a lone /I{GUID} maintenance command is rewritten to /X{GUID}), plus
    /// /qn /quiet /passive /qb /qb! /qb- /norestart and REBOOT=ReallySuppress. Anything else throws.
    /// </summary>
    private static UninstallCommand ParseMsi(IReadOnlyList<string> args, string command)
    {
        var options = new List<string>();
        string? product = null;
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            var m = MsiOperationRx.Match(a);
            if (m.Success)
            {
                if (product is not null) throw new NotSupportedException($"msiexec command has more than one operation: '{command}'");
                if (m.Groups[2].Success) product = m.Groups[2].Value;
                else if (i + 1 < args.Count && GuidArgRx.IsMatch(args[i + 1])) product = args[++i];
                else throw new NotSupportedException($"msiexec /{m.Groups[1].Value} without a product code in '{command}'");
                continue;
            }
            if (MsiAllowedOptions.Contains(a)) { options.Add(a); continue; }
            throw new NotSupportedException($"msiexec option '{a}' is not allowed in '{command}'");
        }
        if (product is null) throw new NotSupportedException($"msiexec command is not an uninstall (/X{{GUID}}): '{command}'");
        product = product.ToUpperInvariant();
        return new UninstallCommand(MsiExecPath, new[] { "/X" + product }.Concat(options).ToList(), true, product);
    }

    /// <summary>Splits arguments with the Windows command-line rules (quotes, doubled quotes, backslashes before quotes).</summary>
    public static IReadOnlyList<string> SplitArguments(string text)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        var have = false;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\')
            {
                var n = 0;
                while (i < text.Length && text[i] == '\\') { n++; i++; }
                if (i < text.Length && text[i] == '"')
                {
                    sb.Append('\\', n / 2);
                    if (n % 2 == 1) { sb.Append('"'); i++; }
                }
                else sb.Append('\\', n);
                have = true;
                continue;
            }
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i += 2; have = true; continue; }
                inQuotes = !inQuotes;
                have = true;
                i++;
                continue;
            }
            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (have) { result.Add(sb.ToString()); sb.Clear(); have = false; }
                i++;
                continue;
            }
            sb.Append(c);
            have = true;
            i++;
        }
        if (inQuotes) throw new ArgumentException($"Unterminated quote in arguments '{text}'", nameof(text));
        if (have) result.Add(sb.ToString());
        return result;
    }

    /// <summary>Spellings of the same legal suffix; suffixes are unified, never dropped, so "Valve GmbH" is not "Valve Corporation".</summary>
    /// <summary>Lowercase, punctuation to spaces, suffix synonyms unified (Corporation = Corp., Inc. = Incorporated, ...).</summary>
    public static string NormaliseName(string name) => Authenticode.NormaliseName(name);

    /// <summary>"Valve Corp." matches "Valve Corporation"; ASUS matches ASUSTeK COMPUTER INC.; case and punctuation are ignored.</summary>
    public static bool NamesMatch(string a, string b) => Authenticode.NamesMatch(a, b);

    internal static IReadOnlyList<string>? TrustedSignerNames(string path) => Authenticode.TrustedSignerNames(path);

    internal static IReadOnlyList<string>? TrustedSignerNames(SafeFileHandle handle, string path) => Authenticode.TrustedSignerNames(handle, path);
}
