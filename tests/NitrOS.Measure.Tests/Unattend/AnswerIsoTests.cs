using System.Text;
using NitrOS.Measure.Unattend;

namespace NitrOS.Measure.Tests.Unattend;

public class AnswerIsoTests : IDisposable
{
    private readonly string _tempIso;

    public AnswerIsoTests()
    {
        _tempIso = Path.Combine(Path.GetTempPath(), "test-answer-" + Guid.NewGuid().ToString("N") + ".iso");
    }

    public void Dispose()
    {
        if (File.Exists(_tempIso))
        {
            try { File.Delete(_tempIso); } catch { }
        }
    }

    [Fact]
    public void AnswerIso_WritesIsoWithFileAtRoot()
    {
        var content = "<?xml version=\"1.0\"?><test>Hello</test>";
        AnswerIso.Write(_tempIso, "autounattend.xml", content);

        Assert.True(File.Exists(_tempIso));

        var bytes = File.ReadAllBytes(_tempIso);
        Assert.True(bytes.Length >= 0x8006);

        // Check standard ISO 9660 identifier "CD001" at offset 0x8001
        var magic = Encoding.ASCII.GetString(bytes, 0x8001, 5);
        Assert.Equal("CD001", magic);

        // Check file name appears in image
        var allText = Encoding.ASCII.GetString(bytes);
        Assert.Contains("AUTOUNAT", allText.ToUpperInvariant());
    }
}
