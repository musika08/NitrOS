namespace NitrOS.Updates.Reading;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NitrOS.Updates.Model;

public sealed class DeviceReader(
    IIdentitySource identity,
    IInstalledDevices devices,
    IFirmwareTable firmware) : IDeviceReader
{
    public DeviceInfo Read()
    {
        string mfg;
        string prod;
        string? baseBoard;
        string? biosVer;
        string? biosDateStr;

        try
        {
            mfg = identity.Manufacturer ?? "";
            prod = identity.ProductName ?? "";
            baseBoard = identity.BaseBoardProduct;
            biosVer = identity.BiosVersion;
            biosDateStr = identity.BiosReleaseDate;
        }
        catch
        {
            mfg = "";
            prod = "";
            baseBoard = null;
            biosVer = null;
            biosDateStr = null;
        }

        var maker = Makers.FromManufacturer(mfg);
        var modelCode = DetermineModelCode(baseBoard, prod);
        var biosDate = ParseBiosDate(biosDateStr);

        IReadOnlyList<InstalledDevice> devList;
        try
        {
            devList = devices.Present() ?? [];
        }
        catch
        {
            devList = [];
        }

        IReadOnlyList<FirmwareResource> fwList;
        try
        {
            fwList = firmware.Read() ?? [];
        }
        catch
        {
            fwList = [];
        }

        return new DeviceInfo(
            maker,
            mfg,
            prod,
            modelCode,
            biosVer,
            biosDate,
            devList,
            fwList);
    }

    private static string? DetermineModelCode(string? baseBoard, string productName)
    {
        if (!string.IsNullOrWhiteSpace(baseBoard) &&
            baseBoard.Any(char.IsLetter) &&
            baseBoard.Any(char.IsDigit) &&
            baseBoard.All(char.IsLetterOrDigit))
        {
            return baseBoard.Trim();
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            var tokens = productName.Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries);
            for (var i = tokens.Length - 1; i >= 0; i--)
            {
                var tok = tokens[i].Trim();
                if (tok.Length > 0 &&
                    tok.Any(char.IsLetter) &&
                    tok.Any(char.IsDigit) &&
                    tok.All(char.IsLetterOrDigit))
                {
                    return tok;
                }
            }
        }

        return null;
    }

    private static DateOnly? ParseBiosDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateOnly.TryParseExact(text.Trim(), "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }
        return null;
    }
}
