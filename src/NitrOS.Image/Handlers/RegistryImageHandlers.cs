namespace NitrOS.Image.Handlers;

using System;
using System.Linq;
using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Offline;
using NitrOS.Image.Systems;
using NitrOS.Live;
using NitrOS.Live.Systems;

public sealed class RegistrySetImageHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var rs = (RegistrySet)action;
        var target = HiveMap.Map(rs.Hive, rs.Key, ctx.CurrentControlSet);

        if (ctx.Hives is null || !ctx.Hives.TryGetValue(target.Hive, out var hive))
        {
            return new ImageActionResult(ImageOutcome.Failed, $"Hive not loaded: {target.Hive}");
        }

        var regVal = new RegValue(rs.Kind, rs.Data);

        if (hive.CanWrite(target.SubKey) == WriteCheck.Refused)
        {
            if (target.Hive == OfflineHive.DefaultUser)
            {
                ctx.FirstBoot.AddUser(new UserRegSet(ctx.SwitchId, $"HKCU\\{target.SubKey}", rs.Name, regVal));
            }
            else
            {
                ctx.FirstBoot.AddMachine(new MachineRegSet(ctx.SwitchId, $"HKLM\\{target.SubKey}", rs.Name, regVal));
            }
            return new ImageActionResult(ImageOutcome.Deferred, "Windows protects this key in the image; it is set when Windows first starts");
        }

        hive.SetValue(target.SubKey, rs.Name, regVal);

        var readBack = hive.GetValue(target.SubKey, rs.Name);
        if (readBack is null || !string.Equals(readBack.Data, rs.Data, StringComparison.OrdinalIgnoreCase) || readBack.Kind != rs.Kind)
        {
            return new ImageActionResult(ImageOutcome.Failed, "readback mismatch");
        }

        return new ImageActionResult(ImageOutcome.Applied);
    }
}

public sealed class RegistryDeleteImageHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var rd = (RegistryDelete)action;
        var target = HiveMap.Map(rd.Hive, rd.Key, ctx.CurrentControlSet);

        if (ctx.Hives is null || !ctx.Hives.TryGetValue(target.Hive, out var hive))
        {
            return new ImageActionResult(ImageOutcome.Failed, $"Hive not loaded: {target.Hive}");
        }

        if (rd.Name is not null)
        {
            if (hive.GetValue(target.SubKey, rd.Name) is null)
            {
                return new ImageActionResult(ImageOutcome.Skipped, "value not present");
            }
        }
        else
        {
            if (!hive.KeyExists(target.SubKey))
            {
                return new ImageActionResult(ImageOutcome.Skipped, "key not present");
            }
        }

        if (hive.CanWrite(target.SubKey) == WriteCheck.Refused)
        {
            if (target.Hive == OfflineHive.DefaultUser)
            {
                ctx.FirstBoot.AddUser(new UserRegDelete(ctx.SwitchId, $"HKCU\\{target.SubKey}", rd.Name));
            }
            else
            {
                ctx.FirstBoot.AddMachine(new MachineRegDelete(ctx.SwitchId, $"HKLM\\{target.SubKey}", rd.Name));
            }
            return new ImageActionResult(ImageOutcome.Deferred, "Windows protects this key in the image; it is set when Windows first starts");
        }

        if (rd.Name is not null)
        {
            hive.DeleteValue(target.SubKey, rd.Name);
        }
        else
        {
            hive.DeleteKey(target.SubKey);
        }

        return new ImageActionResult(ImageOutcome.Applied);
    }
}

public sealed class ServiceStartImageHandler : IImageActionHandler
{
    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var ss = (ServiceStart)action;
        var target = HiveMap.Map("HKLM", $"SYSTEM\\CurrentControlSet\\Services\\{ss.Service}", ctx.CurrentControlSet);

        if (ctx.Hives is null || !ctx.Hives.TryGetValue(OfflineHive.System, out var hive))
        {
            return new ImageActionResult(ImageOutcome.Failed, "SYSTEM hive not loaded");
        }

        if (!hive.KeyExists(target.SubKey))
        {
            return new ImageActionResult(ImageOutcome.Skipped, "service not in image");
        }

        var (startVal, delayed) = ServiceStartValues.For(ss.Start);
        var startRegVal = new RegValue(RegistryKind.Dword, startVal.ToString());
        RegValue? delayedRegVal = delayed.HasValue ? new RegValue(RegistryKind.Dword, delayed.Value.ToString()) : null;

        if (hive.CanWrite(target.SubKey) == WriteCheck.Refused)
        {
            ctx.FirstBoot.AddMachine(new MachineRegSet(ctx.SwitchId, $"HKLM\\{target.SubKey}", "Start", startRegVal));
            if (delayedRegVal is not null)
            {
                ctx.FirstBoot.AddMachine(new MachineRegSet(ctx.SwitchId, $"HKLM\\{target.SubKey}", "DelayedAutostart", delayedRegVal));
            }
            return new ImageActionResult(ImageOutcome.Deferred, "Windows protects this key in the image; it is set when Windows first starts");
        }

        hive.SetValue(target.SubKey, "Start", startRegVal);
        if (delayedRegVal is not null)
        {
            hive.SetValue(target.SubKey, "DelayedAutostart", delayedRegVal);
        }

        return new ImageActionResult(ImageOutcome.Applied);
    }
}

public sealed class StartupDisableImageHandler : IImageActionHandler
{
    private const string DisabledDefaultHex = "030000000000000000000000";

    public ImageActionResult Apply(SwitchAction action, ImageContext ctx)
    {
        var sd = (StartupDisable)action;
        if (sd.Source != StartupSource.Run)
        {
            throw new InvalidOperationException($"Unsupported startup source in image: {sd.Source}");
        }

        if (ctx.Hives is null)
        {
            return new ImageActionResult(ImageOutcome.Failed, "Hives not loaded");
        }

        IOfflineHive hive;
        string runKey;
        string approvedKey;

        if (string.Equals(sd.Hive, "HKCU", StringComparison.OrdinalIgnoreCase))
        {
            if (!ctx.Hives.TryGetValue(OfflineHive.DefaultUser, out hive!))
            {
                return new ImageActionResult(ImageOutcome.Failed, "DefaultUser hive not loaded");
            }
            runKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
            approvedKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run";
        }
        else
        {
            if (!ctx.Hives.TryGetValue(OfflineHive.Software, out hive!))
            {
                return new ImageActionResult(ImageOutcome.Failed, "SOFTWARE hive not loaded");
            }
            runKey = "Microsoft\\Windows\\CurrentVersion\\Run";
            approvedKey = "Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run";
        }

        var valueNames = hive.ValueNames(runKey);
        var matching = valueNames.Where(n => Glob.IsMatch(sd.Name, n)).ToList();

        if (matching.Count == 0)
        {
            return new ImageActionResult(ImageOutcome.Skipped, "no matching startup entry");
        }

        foreach (var name in matching)
        {
            var existing = hive.GetValue(approvedKey, name);
            if (existing is null || string.IsNullOrWhiteSpace(existing.Data))
            {
                hive.SetValue(approvedKey, name, new RegValue(RegistryKind.Binary, DisabledDefaultHex));
            }
            else
            {
                var hex = existing.Data.Trim();
                if (hex.Length >= 2 && hex.StartsWith("02", StringComparison.OrdinalIgnoreCase))
                {
                    var updatedHex = "03" + hex[2..];
                    hive.SetValue(approvedKey, name, new RegValue(RegistryKind.Binary, updatedHex));
                }
            }
        }

        return new ImageActionResult(ImageOutcome.Applied);
    }
}
