using System.Security;
using System.Xml.Linq;

namespace NitrOS.Measure.Unattend;

public static class AnswerFile
{
    public static string Render(
        string template,
        int imageIndex,
        string user,
        string password,
        string computerName)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(computerName);

        var rendered = template
            .Replace("{{IMAGE_INDEX}}", imageIndex.ToString())
            .Replace("{{USER}}", SecurityElement.Escape(user) ?? "")
            .Replace("{{PASSWORD}}", SecurityElement.Escape(password) ?? "")
            .Replace("{{COMPUTER_NAME}}", SecurityElement.Escape(computerName) ?? "");

        if (rendered.Contains("{{"))
        {
            throw new InvalidOperationException("Unrendered placeholders remain in answer file.");
        }

        try
        {
            XDocument.Parse(rendered);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Rendered answer file is not well-formed XML.", ex);
        }

        return rendered;
    }
}
