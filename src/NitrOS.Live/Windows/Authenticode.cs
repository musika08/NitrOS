namespace NitrOS.Live.Windows;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32.SafeHandles;

public enum RevocationMode
{
    OfflineOnly,
    WholeChainOnline
}

public static class Authenticode
{
    private static readonly Dictionary<string, string> SuffixSynonyms = new(StringComparer.Ordinal)
    {
        ["corporation"] = "corp",
        ["incorporated"] = "inc",
        ["company"] = "co",
        ["limited"] = "ltd",
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["asus"] = "asustek computer inc",
    };

    public static string NormaliseName(string name)
    {
        var cleaned = new StringBuilder();
        foreach (var ch in name.ToLowerInvariant()) cleaned.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        var tokens = cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => SuffixSynonyms.TryGetValue(t, out var u) ? u : t);
        var joined = string.Join(' ', tokens);
        return Aliases.TryGetValue(joined, out var canonical) ? canonical : joined;
    }

    public static bool NamesMatch(string a, string b)
    {
        var na = NormaliseName(a);
        return na.Length > 0 && na == NormaliseName(b);
    }

    public static SafeFileHandle OpenLocked(string path) =>
        File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.None);

    public static IReadOnlyList<string>? TrustedSignerNames(string path, RevocationMode mode = RevocationMode.OfflineOnly)
    {
        if (!File.Exists(path)) return null;
        using var h = OpenLocked(path);
        return TrustedSignerNames(h, path, mode);
    }

    public static IReadOnlyList<string>? TrustedSignerNames(SafeFileHandle handle, string path, RevocationMode mode = RevocationMode.OfflineOnly)
    {
        var (hr, names) = VerifyEmbedded(handle, path, mode);
        if (hr == 0) return names;
        if (hr != NativeMethods.TRUST_E_NOSIGNATURE) return null;
        return VerifyCatalog(handle, path, mode);
    }

    public static bool SignedBy(string path, string organisation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisation);
        var signers = TrustedSignerNames(path);
        if (signers is null || signers.Count == 0) return false;

        return signers.Any(s => s.Contains(organisation, StringComparison.OrdinalIgnoreCase));
    }

    private static (int Hr, IReadOnlyList<string>? Names) VerifyEmbedded(SafeFileHandle handle, string path, RevocationMode mode)
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
            return Verify(NativeMethods.WTD_CHOICE_FILE, infoPtr, mode);
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
            Marshal.FreeHGlobal(pathPtr);
            if (added) handle.DangerousRelease();
        }
    }

    private static IReadOnlyList<string>? VerifyCatalog(SafeFileHandle fh, string path, RevocationMode mode)
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
                    var (hr, names) = VerifyCatalogMember(fh, path, ci.wszCatalogFile, hash, admin, mode);
                    return hr == 0 ? names : null;
                }
                finally { NativeMethods.CryptCATAdminReleaseCatalogContext(admin, catInfo, 0); }
            }
            finally { NativeMethods.CryptCATAdminReleaseContext(admin, 0); }
        }
        return null;
    }

    private static (int Hr, IReadOnlyList<string>? Names) VerifyCatalogMember(SafeFileHandle fh, string path, string catalogFile, byte[] hash, IntPtr admin, RevocationMode mode)
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
            return Verify(NativeMethods.WTD_CHOICE_CATALOG, infoPtr, mode);
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

    private static (int Hr, IReadOnlyList<string>? Names) Verify(uint unionChoice, IntPtr info, RevocationMode mode)
    {
        var action = NativeMethods.WINTRUST_ACTION_GENERIC_VERIFY_V2;
        var (revokeChecks, provFlags) = mode == RevocationMode.WholeChainOnline
            ? (NativeMethods.WTD_REVOKE_WHOLECHAIN, NativeMethods.WTD_REVOCATION_CHECK_CHAIN)
            : (NativeMethods.WTD_REVOKE_NONE, NativeMethods.WTD_REVOCATION_CHECK_NONE | NativeMethods.WTD_CACHE_ONLY_URL_RETRIEVAL);

        var data = new NativeMethods.WINTRUST_DATA
        {
            cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_DATA>(),
            dwUIChoice = NativeMethods.WTD_UI_NONE,
            fdwRevocationChecks = revokeChecks,
            dwUnionChoice = unionChoice,
            pInfo = info,
            dwStateAction = NativeMethods.WTD_STATEACTION_VERIFY,
            dwProvFlags = provFlags,
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
