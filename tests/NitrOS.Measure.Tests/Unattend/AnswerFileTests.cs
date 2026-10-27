using NitrOS.Measure.Unattend;

namespace NitrOS.Measure.Tests.Unattend;

public class AnswerFileTests
{
    [Fact]
    public void AnswerFile_ReplacesEveryPlaceholderAndEscapes()
    {
        var template = """
            <?xml version="1.0" encoding="utf-8"?>
            <unattend>
              <index>{{IMAGE_INDEX}}</index>
              <user>{{USER}}</user>
              <pwd>{{PASSWORD}}</pwd>
              <pc>{{COMPUTER_NAME}}</pc>
            </unattend>
            """;

        var rendered = AnswerFile.Render(template, 2, "test<user>", "p&w\"d>", "MY-PC");

        Assert.Contains("<index>2</index>", rendered);
        Assert.Contains("<user>test&lt;user&gt;</user>", rendered);
        Assert.Contains("<pwd>p&amp;w&quot;d&gt;</pwd>", rendered);
        Assert.Contains("<pc>MY-PC</pc>", rendered);
        Assert.DoesNotContain("{{", rendered);
    }

    [Fact]
    public void AnswerFile_TemplateInRepoRendersToValidXml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        var templatePath = Path.Combine(dir!.FullName, "measure", "unattend", "autounattend.template.xml");
        var template = File.ReadAllText(templatePath);

        var rendered = AnswerFile.Render(template, 1, "nitros-test", "P@ssw0rd!123", "NITROS-TEST");

        Assert.NotEmpty(rendered);
        Assert.DoesNotContain("{{", rendered);
        Assert.Contains("nitros-test", rendered);
    }
}
