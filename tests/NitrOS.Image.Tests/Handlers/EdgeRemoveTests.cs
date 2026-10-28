namespace NitrOS.Image.Tests.Handlers;

using System.Collections.Generic;
using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Handlers;
using NitrOS.Image.Offline;
using NitrOS.Image.Systems;
using NitrOS.Image.Tests.Fakes;
using NitrOS.Live.Systems;

public sealed class EdgeRemoveTests
{
    [Fact]
    public void EdgeRemove_DeletesUninstallEntriesWhateverTheFolderDid()
    {
        // Case 1: Folder absent
        {
            var fakes = new FakeImageSystems();
            var softwareHive = (FakeOfflineRegistry.FakeHive)fakes.Registry.Load("C:\\SOFTWARE", "NitrOS.Image.SOFTWARE");
            softwareHive.SetValue("Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge", "DisplayName", new RegValue(RegistryKind.String, "Microsoft Edge"));
            softwareHive.SetValue("WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge", "DisplayName", new RegValue(RegistryKind.String, "Microsoft Edge"));

            var hives = new Dictionary<OfflineHive, IOfflineHive>
            {
                [OfflineHive.Software] = softwareHive
            };

            var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "edge-remove")
            {
                Hives = hives
            };

            // edge-remove has file.remove and two registry.delete
            var actions = new SwitchAction[]
            {
                new FileRemove("%ProgramFiles(x86)%\\Microsoft\\Edge"),
                new RegistryDelete("HKLM", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge"),
                new RegistryDelete("HKLM", "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge")
            };

            foreach (var action in actions)
            {
                ImageHandlers.For(action).Apply(action, ctx);
            }

            Assert.False(softwareHive.KeyExists("Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge"));
            Assert.False(softwareHive.KeyExists("WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge"));
        }

        // Case 2: DeleteUnder throws
        {
            var fakes = new FakeImageSystems();
            var softwareHive = (FakeOfflineRegistry.FakeHive)fakes.Registry.Load("C:\\SOFTWARE", "NitrOS.Image.SOFTWARE");
            softwareHive.SetValue("Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge", "DisplayName", new RegValue(RegistryKind.String, "Microsoft Edge"));
            softwareHive.SetValue("WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge", "DisplayName", new RegValue(RegistryKind.String, "Microsoft Edge"));

            fakes.Files.Files["M:\\mount\\Program Files (x86)\\Microsoft\\Edge\\file.txt"] = "dummy";
            fakes.Files.FailOn = nameof(IImageFiles.DeleteUnder);

            var hives = new Dictionary<OfflineHive, IOfflineHive>
            {
                [OfflineHive.Software] = softwareHive
            };

            var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, new FirstBootPlan(), "edge-remove")
            {
                Hives = hives
            };

            var actions = new SwitchAction[]
            {
                new FileRemove("%ProgramFiles(x86)%\\Microsoft\\Edge"),
                new RegistryDelete("HKLM", "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge"),
                new RegistryDelete("HKLM", "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge")
            };

            foreach (var action in actions)
            {
                try
                {
                    ImageHandlers.For(action).Apply(action, ctx);
                }
                catch
                {
                    // File delete threw, continue to registry
                }
            }

            Assert.False(softwareHive.KeyExists("Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge"));
            Assert.False(softwareHive.KeyExists("WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft Edge"));
        }
    }
}
