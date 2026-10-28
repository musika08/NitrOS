namespace NitrOS.Updates.Model;

using System;
using NitrOS.Live.Windows;

public enum MakerId
{
    Asus,
    Msi,
    Lenovo,
    Dell,
    Hp,
    Other
}

public static class Makers
{
    public static MakerId FromManufacturer(string manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer)) return MakerId.Other;

        if (Authenticode.NamesMatch(manufacturer, "ASUSTeK COMPUTER INC.") ||
            manufacturer.Contains("ASUS", StringComparison.OrdinalIgnoreCase))
        {
            return MakerId.Asus;
        }

        if (Authenticode.NamesMatch(manufacturer, "Micro-Star International Co., Ltd.") ||
            manufacturer.Contains("MSI", StringComparison.OrdinalIgnoreCase))
        {
            return MakerId.Msi;
        }

        if (Authenticode.NamesMatch(manufacturer, "LENOVO") ||
            manufacturer.Contains("LENOVO", StringComparison.OrdinalIgnoreCase))
        {
            return MakerId.Lenovo;
        }

        if (Authenticode.NamesMatch(manufacturer, "Dell Inc.") ||
            manufacturer.Contains("Dell", StringComparison.OrdinalIgnoreCase))
        {
            return MakerId.Dell;
        }

        if (Authenticode.NamesMatch(manufacturer, "HP") ||
            Authenticode.NamesMatch(manufacturer, "Hewlett-Packard") ||
            manufacturer.Contains("HP", StringComparison.OrdinalIgnoreCase) ||
            manufacturer.Contains("Hewlett-Packard", StringComparison.OrdinalIgnoreCase))
        {
            return MakerId.Hp;
        }

        return MakerId.Other;
    }
}
