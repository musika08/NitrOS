namespace NitrOS.App.Tests;

using System;
using System.IO;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Live.Windows;

public sealed class IsoBuildIndexTests
{
    [Fact]
    public void IsoBuildIndex_ReadNeverCreatesFolder()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros-index-read-" + Guid.NewGuid().ToString("N"));
        var created = false;
        var session = new FakeSession(elevated: true);
        var index = new IsoBuildIndex(
            temp,
            _ => FolderState.Missing,
            _ => created = true,
            session);

        var entry = index.Read();
        Assert.Null(entry);
        Assert.False(created);
        Assert.False(Directory.Exists(temp));
    }

    [Fact]
    public void IsoBuildIndex_WriteRefusedWhenNotElevated()
    {
        var temp = Path.Combine(Path.GetTempPath(), "nitros-index-elev-" + Guid.NewGuid().ToString("N"));
        var session = new FakeSession(elevated: false);
        var index = new IsoBuildIndex(
            temp,
            _ => FolderState.Missing,
            _ => Directory.CreateDirectory(temp),
            session);

        var entry = new IsoBuildRecord("C:\\out.iso", "C:\\work", "123", CardId.Recommended, ["sw1"]);
        Assert.Throws<UnauthorizedAccessException>(() => index.Set(entry));
        Assert.Throws<UnauthorizedAccessException>(() => index.Clear());
    }
}
