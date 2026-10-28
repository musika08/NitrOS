namespace NitrOS.Updates.Windows;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using global::Windows.Devices.Enumeration.Pnp;
using NitrOS.Updates.Model;
using NitrOS.Updates.Reading;

public sealed class WinInstalledDevices : IInstalledDevices
{
    private static readonly string[] RequestedProperties =
    [
        "System.Devices.HardwareIds",
        "System.Devices.CompatibleIds",
        "{a8b865dd-2e3d-4094-ad97-e593a70c75d6} 3", // DEVPKEY_Device_DriverVersion
        "{a8b865dd-2e3d-4094-ad97-e593a70c75d6} 2", // DEVPKEY_Device_DriverDate
        "{a8b865dd-2e3d-4094-ad97-e593a70c75d6} 9", // DEVPKEY_Device_DriverProvider
        "{a45c254e-df1c-4efd-8020-67d146a850e0} 9", // DEVPKEY_Device_Class
        "{a45c254e-df1c-4efd-8020-67d146a850e0} 14", // DEVPKEY_Device_FriendlyName
        "{a45c254e-df1c-4efd-8020-67d146a850e0} 2",  // DEVPKEY_Device_DeviceDesc
        "{4340a6c5-93fa-4706-972c-7b648008a5a7} 1"   // DEVPKEY_Device_IsPresent
    ];

    public IReadOnlyList<InstalledDevice> Present()
    {
        var task = Task.Run(async () =>
        {
            var pnpObjects = await PnpObject.FindAllAsync(PnpObjectType.Device, RequestedProperties);
            var result = new List<InstalledDevice>();

            foreach (var pnp in pnpObjects)
            {
                if (pnp.Properties.TryGetValue("{4340a6c5-93fa-4706-972c-7b648008a5a7} 1", out var isPresentObj))
                {
                    if (isPresentObj is bool isPres && !isPres) continue;
                }

                var instanceId = pnp.Id;
                var friendlyName = pnp.Properties.TryGetValue("{a45c254e-df1c-4efd-8020-67d146a850e0} 14", out var fnObj) ? fnObj as string : null;
                var desc = pnp.Properties.TryGetValue("{a45c254e-df1c-4efd-8020-67d146a850e0} 2", out var descObj) ? descObj as string : null;
                var name = !string.IsNullOrWhiteSpace(friendlyName) ? friendlyName : (!string.IsNullOrWhiteSpace(desc) ? desc : instanceId);

                var deviceClass = pnp.Properties.TryGetValue("{a45c254e-df1c-4efd-8020-67d146a850e0} 9", out var clsObj) ? clsObj as string : null;

                var hwIds = new List<string>();
                if (pnp.Properties.TryGetValue("System.Devices.HardwareIds", out var hwObj) && hwObj is string[] hwArr)
                {
                    hwIds.AddRange(hwArr);
                }

                var driverVersion = pnp.Properties.TryGetValue("{a8b865dd-2e3d-4094-ad97-e593a70c75d6} 3", out var verObj) ? verObj as string : null;
                var driverProvider = pnp.Properties.TryGetValue("{a8b865dd-2e3d-4094-ad97-e593a70c75d6} 9", out var provObj) ? provObj as string : null;

                DateOnly? driverDate = null;
                if (pnp.Properties.TryGetValue("{a8b865dd-2e3d-4094-ad97-e593a70c75d6} 2", out var dateObj))
                {
                    if (dateObj is DateTimeOffset dto) driverDate = DateOnly.FromDateTime(dto.DateTime);
                    else if (dateObj is DateTime dt) driverDate = DateOnly.FromDateTime(dt);
                }

                result.Add(new InstalledDevice(
                    instanceId,
                    name,
                    deviceClass,
                    hwIds,
                    driverVersion,
                    driverDate,
                    driverProvider));
            }

            return (IReadOnlyList<InstalledDevice>)result;
        });

        if (!task.Wait(TimeSpan.FromSeconds(60)))
        {
            throw new TimeoutException("Timed out querying Pnp devices after 60 seconds.");
        }

        return task.Result;
    }
}
