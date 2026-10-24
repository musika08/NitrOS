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
        var cmd = Parse(Compose(entry, args));
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
    internal static SafeFileHandle OpenLocked(string path) =>
        File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);

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
    private static readonly Dictionary<string, string> SuffixSynonyms = new(StringComparer.Ordinal)
    {
        ["corporation"] = "corp",
        ["incorporated"] = "inc",
        ["company"] = "co",
        ["limited"] = "ltd",
    };

    /// <summary>Explicit spellings of the same company, after normalisation. Nothing is matched by prefix or similarity.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["asus"] = "asustek computer inc",
    };

    /// <summary>Lowercase, punctuation to spaces, suffix synonyms unified (Corporation = Corp., Inc. = Incorporated, ...).</summary>
    public static string NormaliseName(string name)
    {
        var cleaned = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant()) cleaned.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        var tokens = cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => SuffixSynonyms.TryGetValue(t, out var u) ? u : t);
        var joined = string.Join(' ', tokens);
        return Aliases.TryGetValue(joined, out var canonical) ? canonical : joined;
    }

    /// <summary>"Valve Corp." matches "Valve Corporation"; ASUS matches ASUSTeK COMPUTER INC.; case and punctuation are ignored.</summary>
    public static bool NamesMatch(string a, string b)
    {
        var na = NormaliseName(a);
        return na.Length > 0 && na == NormaliseName(b);
    }

    /// <summary>
    /// Verifies the file with WinVerifyTrust (embedded signature, else the system catalog), with no UI and no
    /// revocation network lookups. Returns the leaf signer's common name and organisation, or null when not trusted.
    /// </summary>
    internal static IReadOnlyList<string>? TrustedSignerNames(string path)
    {
        if (!File.Exists(path)) return null;
        using var h = OpenLocked(path);
        return TrustedSignerNames(h, path);
    }

    /// <summary>As above, reading through an already open handle (WINTRUST_FILE_INFO.hFile, catalog hash and member handle).</summary>
    internal static IReadOnlyList<string>? TrustedSignerNames(SafeFileHandle handle, string path)
    {
        var (hr, names) = VerifyEmbedded(handle, path);
        if (hr == 0) return names;
        if (hr != NativeMethods.TRUST_E_NOSIGNATURE) return null;
        return VerifyCatalog(handle, path);
    }

    private static (int Hr, IReadOnlyList<string>? Names) VerifyEmbedded(SafeFileHandle handle, string path)
    {
        var added = false;
        handle.DangerousAddRef(ref added);
        var pathPtr = Marshal.StringToHGlobalUni(path);
        var infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.WINTRUST_FILE_INFO>());
        try
        {
            var info = new NativeMethods.WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_FILE_INFO>(),
                pcwszFilePath = pathPtr,
                hFile = handle.DangerousGetHandle(),
            };
            Marshal.StructureToPtr(info, infoPtr, false);
            return Verify(NativeMethods.WTD_CHOICE_FILE, infoPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
            Marshal.FreeHGlobal(pathPtr);
            if (added) handle.DangerousRelease();
        }
    }

    private static IReadOnlyList<string>? VerifyCatalog(SafeFileHandle fh, string path)
    {
        foreach (var algorithm in new[] { "SHA256", null })
        {
            if (!NativeMethods.CryptCATAdminAcquireContext2(out var admin, IntPtr.Zero, algorithm, IntPtr.Zero, 0)) continue;
            try
            {
                uint size = 0;
                NativeMethods.CryptCATAdminCalcHashFromFileHandle2(admin, fh, ref size, null, 0);
                if (size == 0 || size > 1024) continue;
                var hash = new byte[size];
                if (!NativeMethods.CryptCATAdminCalcHashFromFileHandle2(admin, fh, ref size, hash, 0)) continue;
                var catInfo = NativeMethods.CryptCATAdminEnumCatalogFromHash(admin, hash, (uint)hash.Length, 0, IntPtr.Zero);
                if (catInfo == IntPtr.Zero) continue;
                try
                {
                    var ci = new NativeMethods.CATALOG_INFO { cbStruct = (uint)Marshal.SizeOf<NativeMethods.CATALOG_INFO>(), wszCatalogFile = "" };
                    if (!NativeMethods.CryptCATCatalogInfoFromContext(catInfo, ref ci, 0)) continue;
                    var (hr, names) = VerifyCatalogMember(fh, path, ci.wszCatalogFile, hash, admin);
                    return hr == 0 ? names : null;
                }
                finally { NativeMethods.CryptCATAdminReleaseCatalogContext(admin, catInfo, 0); }
            }
            finally { NativeMethods.CryptCATAdminReleaseContext(admin, 0); }
        }
        return null;
    }

    private static (int Hr, IReadOnlyList<string>? Names) VerifyCatalogMember(SafeFileHandle fh, string path, string catalogFile, byte[] hash, IntPtr admin)
    {
        var added = false;
        fh.DangerousAddRef(ref added);
        var catPtr = Marshal.StringToHGlobalUni(catalogFile);
        var tagPtr = Marshal.StringToHGlobalUni(Convert.ToHexString(hash));
        var memberPtr = Marshal.StringToHGlobalUni(path);
        var hashPtr = Marshal.AllocHGlobal(hash.Length);
        var infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.WINTRUST_CATALOG_INFO>());
        try
        {
            Marshal.Copy(hash, 0, hashPtr, hash.Length);
            var info = new NativeMethods.WINTRUST_CATALOG_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_CATALOG_INFO>(),
                pcwszCatalogFilePath = catPtr,
                pcwszMemberTag = tagPtr,
                pcwszMemberFilePath = memberPtr,
                hMemberFile = fh.DangerousGetHandle(),
                pbCalculatedFileHash = hashPtr,
                cbCalculatedFileHash = (uint)hash.Length,
                hCatAdmin = admin,
            };
            Marshal.StructureToPtr(info, infoPtr, false);
            return Verify(NativeMethods.WTD_CHOICE_CATALOG, infoPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
            Marshal.FreeHGlobal(hashPtr);
            Marshal.FreeHGlobal(memberPtr);
            Marshal.FreeHGlobal(tagPtr);
            Marshal.FreeHGlobal(catPtr);
            if (added) fh.DangerousRelease();
        }
    }

    private static (int Hr, IReadOnlyList<string>? Names) Verify(uint unionChoice, IntPtr info)
    {
        var action = NativeMethods.WINTRUST_ACTION_GENERIC_VERIFY_V2;
        var data = new NativeMethods.WINTRUST_DATA
        {
            cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_DATA>(),
            dwUIChoice = NativeMethods.WTD_UI_NONE,
            fdwRevocationChecks = NativeMethods.WTD_REVOKE_NONE,
            dwUnionChoice = unionChoice,
            pInfo = info,
            dwStateAction = NativeMethods.WTD_STATEACTION_VERIFY,
            dwProvFlags = NativeMethods.WTD_REVOCATION_CHECK_NONE | NativeMethods.WTD_CACHE_ONLY_URL_RETRIEVAL,
        };
        var noUi = new IntPtr(-1);
        var hr = NativeMethods.WinVerifyTrust(noUi, ref action, ref data);
        try
        {
            return hr == 0 ? (0, LeafSignerNames(data.hWVTStateData)) : (hr, null);
        }
        finally
        {
            data.dwStateAction = NativeMethods.WTD_STATEACTION_CLOSE;
            NativeMethods.WinVerifyTrust(noUi, ref action, ref data);
        }
    }

    private static IReadOnlyList<string> LeafSignerNames(IntPtr state)
    {
        var prov = NativeMethods.WTHelperProvDataFromStateData(state);
        if (prov == IntPtr.Zero) throw new InvalidOperationException("WinVerifyTrust returned no provider data");
        var signer = NativeMethods.WTHelperGetProvSignerFromChain(prov, 0, false, 0);
        if (signer == IntPtr.Zero) throw new InvalidOperationException("WinVerifyTrust returned no signer");
        var provCert = NativeMethods.WTHelperGetProvCertFromChain(signer, 0);
        if (provCert == IntPtr.Zero) throw new InvalidOperationException("WinVerifyTrust returned no signer certificate");
        var certContext = Marshal.ReadIntPtr(provCert, IntPtr.Size);
        if (certContext == IntPtr.Zero) throw new InvalidOperationException("WinVerifyTrust returned an empty signer certificate");

        using var cert = new X509Certificate2(certContext);
        var names = new List<string>();
        var cn = cert.GetNameInfo(X509NameType.SimpleName, false);
        if (!string.IsNullOrWhiteSpace(cn)) names.Add(cn);
        foreach (var rdn in cert.SubjectName.EnumerateRelativeDistinguishedNames())
            if (rdn.HasMultipleElements == false && rdn.GetSingleElementType().Value == "2.5.4.10" && rdn.GetSingleElementValue() is { Length: > 0 } o)
                names.Add(o);
        return names;
    }
}
