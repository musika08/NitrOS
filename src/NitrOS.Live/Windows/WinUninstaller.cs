using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>A parsed uninstall command: the program to start and its separate arguments.</summary>
public sealed record UninstallCommand(string Executable, IReadOnlyList<string> Arguments, bool IsMsiExec);

/// <summary>
/// Reads the Uninstall keys (HKLM 64-bit, HKLM 32-bit, HKCU; both registry views), checks the uninstaller's
/// Authenticode signer against the publisher, and runs the uninstaller with a separate argument list.
/// </summary>
public sealed class WinUninstaller : IUninstaller
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(30);
    private static readonly Regex UnquotedExeRx = new(@"^(.+?\.exe)(?=\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MsiUninstallRx = new(@"^[/-]x(\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\})?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GuidArgRx = new(@"^\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$", RegexOptions.Compiled);
    // msiexec operations other than uninstall: install, admin, advertise, patch, update, repair.
    private static readonly Regex MsiOtherOperationRx = new(@"^[/-](i|a|j|ju|jm|p|update|f[poedcamusv]*)(\{.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

    /// <summary>HKCU\Software is shared between the views, so its two views get one label and are de-duplicated.</summary>
    private static IEnumerable<(RegistryHive, RegistryView, string)> Roots()
    {
        yield return (RegistryHive.LocalMachine, RegistryView.Registry64, @"HKLM\" + UninstallKey);
        yield return (RegistryHive.LocalMachine, RegistryView.Registry32, @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        yield return (RegistryHive.CurrentUser, RegistryView.Registry64, @"HKCU\" + UninstallKey);
        yield return (RegistryHive.CurrentUser, RegistryView.Registry32, @"HKCU\" + UninstallKey);
    }

    public bool SignerMatches(UninstallEntry entry, string publisher)
    {
        UninstallCommand cmd;
        try { cmd = Parse(Compose(entry, null)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException) { return false; }

        if (cmd.IsMsiExec)
        {
            // msiexec.exe is catalog-signed by Microsoft; the MSI product itself is identified by the entry's publisher.
            var msiSigners = TrustedSignerNames(cmd.Executable);
            return msiSigners is not null && msiSigners.Any(n => NamesMatch(n, "Microsoft Corporation")) && NamesMatch(entry.Publisher, publisher);
        }
        if (!File.Exists(cmd.Executable)) return false;
        var signers = TrustedSignerNames(cmd.Executable);
        return signers is not null && signers.Any(n => NamesMatch(n, publisher));
    }

    public int Run(UninstallEntry entry, string? args)
    {
        var cmd = Parse(Compose(entry, args));
        var (code, _, _) = ProcessRunner.Run(cmd.Executable, cmd.Arguments, RunTimeout);
        return code;
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

    private static UninstallCommand ParseMsi(IReadOnlyList<string> args, string command)
    {
        var outArgs = new List<string>();
        var uninstalls = 0;
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            var m = MsiUninstallRx.Match(a);
            if (m.Success)
            {
                uninstalls++;
                if (m.Groups[1].Success) { outArgs.Add(a); continue; }
                // "/X {GUID}" written as two tokens
                if (i + 1 < args.Count && GuidArgRx.IsMatch(args[i + 1])) { outArgs.Add(a); outArgs.Add(args[++i]); continue; }
                throw new NotSupportedException($"msiexec /X without a product code in '{command}'");
            }
            if (MsiOtherOperationRx.IsMatch(a)) throw new NotSupportedException($"msiexec command is not an uninstall: '{command}'");
            outArgs.Add(a);
        }
        if (uninstalls != 1) throw new NotSupportedException($"msiexec command is not a single uninstall (/X{{GUID}}): '{command}'");
        return new UninstallCommand(MsiExecPath, outArgs, true);
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

    private static readonly HashSet<string> LegalSuffixes = new(StringComparer.Ordinal)
    {
        "corporation", "corp", "incorporated", "inc", "limited", "ltd", "llc", "co", "company", "gmbh", "ag", "plc", "sa", "bv", "srl", "kk",
    };

    /// <summary>Explicit spellings of the same company, after normalisation. Nothing is matched by prefix or similarity.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["asus"] = "asustek computer",
    };

    /// <summary>Lowercase, punctuation to spaces, trailing legal suffixes (Corp., Inc., Ltd.) dropped.</summary>
    public static string NormaliseName(string name)
    {
        var cleaned = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant()) cleaned.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        var tokens = cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (tokens.Count > 1 && LegalSuffixes.Contains(tokens[^1])) tokens.RemoveAt(tokens.Count - 1);
        var joined = string.Join(' ', tokens);
        return Aliases.TryGetValue(joined, out var canonical) ? canonical : joined;
    }

    /// <summary>"Valve Corp." matches "Valve Corporation"; ASUS matches ASUSTeK COMPUTER INC.; case is ignored.</summary>
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
        var (hr, names) = VerifyEmbedded(path);
        if (hr == 0) return names;
        if (hr != NativeMethods.TRUST_E_NOSIGNATURE) return null;
        return VerifyCatalog(path);
    }

    private static (int Hr, IReadOnlyList<string>? Names) VerifyEmbedded(string path)
    {
        var pathPtr = Marshal.StringToHGlobalUni(path);
        var info = new NativeMethods.WINTRUST_FILE_INFO
        {
            cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_FILE_INFO>(),
            pcwszFilePath = pathPtr,
        };
        var infoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.WINTRUST_FILE_INFO>());
        try
        {
            Marshal.StructureToPtr(info, infoPtr, false);
            return Verify(NativeMethods.WTD_CHOICE_FILE, infoPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
            Marshal.FreeHGlobal(pathPtr);
        }
    }

    private static IReadOnlyList<string>? VerifyCatalog(string path)
    {
        foreach (var algorithm in new[] { "SHA256", null })
        {
            if (!NativeMethods.CryptCATAdminAcquireContext2(out var admin, IntPtr.Zero, algorithm, IntPtr.Zero, 0)) continue;
            try
            {
                byte[] hash;
                using (var fh = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    uint size = 0;
                    NativeMethods.CryptCATAdminCalcHashFromFileHandle2(admin, fh, ref size, null, 0);
                    if (size == 0 || size > 1024) continue;
                    hash = new byte[size];
                    if (!NativeMethods.CryptCATAdminCalcHashFromFileHandle2(admin, fh, ref size, hash, 0)) continue;
                }
                var catInfo = NativeMethods.CryptCATAdminEnumCatalogFromHash(admin, hash, (uint)hash.Length, 0, IntPtr.Zero);
                if (catInfo == IntPtr.Zero) continue;
                try
                {
                    var ci = new NativeMethods.CATALOG_INFO { cbStruct = (uint)Marshal.SizeOf<NativeMethods.CATALOG_INFO>(), wszCatalogFile = "" };
                    if (!NativeMethods.CryptCATCatalogInfoFromContext(catInfo, ref ci, 0)) continue;
                    var (hr, names) = VerifyCatalogMember(path, ci.wszCatalogFile, hash, admin);
                    if (hr == 0) return names;
                    return null;
                }
                finally { NativeMethods.CryptCATAdminReleaseCatalogContext(admin, catInfo, 0); }
            }
            finally { NativeMethods.CryptCATAdminReleaseContext(admin, 0); }
        }
        return null;
    }

    private static (int Hr, IReadOnlyList<string>? Names) VerifyCatalogMember(string path, string catalogFile, byte[] hash, IntPtr admin)
    {
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
