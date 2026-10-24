using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

public class WinRestorePointTests
{
    private static bool VmEnabled => Environment.GetEnvironmentVariable("NITROS_VM") == "1";

    [Fact]
    public void ParseNewest_TakesLatestUtcTime()
    {
        var text = "2026-10-20T10:15:00.0000000Z\r\n2026-10-24T08:00:00.0000000Z\r\n\r\n2026-10-22T23:59:59.0000000Z\r\n";
        Assert.Equal(new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc), WinRestorePoint.ParseNewest(text));
        Assert.Null(WinRestorePoint.ParseNewest(" \r\n"));
        Assert.ThrowsAny<FormatException>(() => WinRestorePoint.ParseNewest("not a date"));
    }

    [Fact]
    public void Classify_NewerPointMeansCreated()
    {
        var t = new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);
        Assert.Equal(RestorePointOutcome.Created, WinRestorePoint.Classify(null, t));
        Assert.Equal(RestorePointOutcome.Created, WinRestorePoint.Classify(t, t.AddMinutes(1)));
        Assert.Equal(RestorePointOutcome.SkippedRecent, WinRestorePoint.Classify(t, t));
        Assert.Equal(RestorePointOutcome.SkippedRecent, WinRestorePoint.Classify(null, null));
    }

    [Fact]
    public void Finish_UnreadableRestorePointListIsCreatedUnknownNotOff()
    {
        var t = new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);
        Assert.Equal(RestorePointOutcome.CreatedUnknown,
            WinRestorePoint.Finish(t, true, () => throw new InvalidOperationException("Reading restore points failed (exit 1)")));
        Assert.Equal(RestorePointOutcome.CreatedUnknown, WinRestorePoint.Finish(null, false, () => t));
        Assert.Equal(RestorePointOutcome.Created, WinRestorePoint.Finish(t, true, () => t.AddMinutes(1)));
        Assert.Equal(RestorePointOutcome.SkippedRecent, WinRestorePoint.Finish(t, true, () => t));
    }

    [Fact]
    public void RestorePointStructs_MatchPackedNativeLayout()
    {
        // srrestoreptapi.h uses pack(1): 4 + 4 + 8 + 256 * 2 and 4 + 8.
        Assert.Equal(528, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RESTOREPOINTINFOW>());
        Assert.Equal(12, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.STATEMGRSTATUS>());
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void WinRestorePoint_Create()
    {
        Assert.SkipUnless(VmEnabled, "VM lane only: creates a real restore point (set NITROS_VM=1 inside the VM)");
        var outcome = new WinRestorePoint().Create("NitrOS VM test");
        Assert.Contains(outcome, new[] { RestorePointOutcome.Created, RestorePointOutcome.SkippedRecent, RestorePointOutcome.Unavailable });
    }
}
