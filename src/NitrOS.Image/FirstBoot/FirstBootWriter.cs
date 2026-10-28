namespace NitrOS.Image.FirstBoot;

using System;
using System.IO;
using System.Text;
using NitrOS.Core.Model;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;

public static class FirstBootWriter
{
    public const string Folder = @"Windows\Setup\Scripts\NitrOS";
    public const string SetupCompletePath = @"Windows\Setup\Scripts\SetupComplete.cmd";
    public const string BeginMarker = "rem NitrOS begin";
    public const string EndMarker = "rem NitrOS end";
    public const string MachineRunOnceName = "NitrOS.FirstBoot";
    public const string UserRunOnceName = "NitrOS.FirstLogon";

    private const string Block = "rem NitrOS begin\r\ncall \"%~dp0NitrOS\\machine.cmd\" SetupComplete\r\nrem NitrOS end\r\n";

    public static void WriteFiles(RenderedScripts r, string mountDir, IImageFiles files)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountDir);
        ArgumentNullException.ThrowIfNull(files);

        var nitrosDir = Path.Combine(mountDir, Folder);

        if (r.MachineCmd is not null)
        {
            var machineCmdPath = Path.Combine(nitrosDir, "machine.cmd");
            files.WriteText(machineCmdPath, r.MachineCmd);

            foreach (var (fileName, body) in r.ScriptFiles)
            {
                var scriptPath = Path.Combine(nitrosDir, fileName);
                files.WriteText(scriptPath, body);
            }

            var setupCompletePath = Path.Combine(mountDir, SetupCompletePath);
            if (files.Exists(setupCompletePath))
            {
                var existing = files.ReadText(setupCompletePath) ?? "";
                var updated = ReplaceOrAppendBlock(existing);
                files.WriteText(setupCompletePath, updated);
            }
            else
            {
                files.WriteText(setupCompletePath, Block);
            }
        }

        if (r.UserCmd is not null)
        {
            var userCmdPath = Path.Combine(nitrosDir, "user.cmd");
            files.WriteText(userCmdPath, r.UserCmd);
        }
    }

    private static string ReplaceOrAppendBlock(string content)
    {
        var beginIdx = content.IndexOf(BeginMarker, StringComparison.Ordinal);
        if (beginIdx >= 0)
        {
            var endIdx = content.IndexOf(EndMarker, beginIdx, StringComparison.Ordinal);
            if (endIdx >= 0)
            {
                var lineEnd = content.IndexOf("\n", endIdx, StringComparison.Ordinal);
                var removeEnd = lineEnd >= 0 ? lineEnd + 1 : content.Length;
                var before = content[..beginIdx];
                var after = content[removeEnd..];
                return before + Block + after;
            }
        }

        var sb = new StringBuilder(content);
        if (content.Length > 0 && !content.EndsWith("\n") && !content.EndsWith("\r"))
        {
            sb.AppendLine();
        }
        sb.Append(Block);
        return sb.ToString();
    }

    public static void WriteRunOnce(RenderedScripts r, IOfflineHive software, IOfflineHive defaultUser)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(software);
        ArgumentNullException.ThrowIfNull(defaultUser);

        if (r.MachineCmd is not null)
        {
            const string cmdLine = @"%SystemRoot%\System32\cmd.exe /d /c %SystemRoot%\Setup\Scripts\NitrOS\machine.cmd RunOnce";
            if (cmdLine.Length > 260)
            {
                throw new InvalidOperationException($"Machine RunOnce command line exceeds 260 chars: {cmdLine}");
            }

            software.SetValue(
                @"Microsoft\Windows\CurrentVersion\RunOnce",
                MachineRunOnceName,
                new RegValue(RegistryKind.ExpandString, cmdLine));
        }

        if (r.UserCmd is not null)
        {
            const string cmdLine = @"%SystemRoot%\System32\cmd.exe /d /c %SystemRoot%\Setup\Scripts\NitrOS\user.cmd";
            if (cmdLine.Length > 260)
            {
                throw new InvalidOperationException($"User RunOnce command line exceeds 260 chars: {cmdLine}");
            }

            defaultUser.SetValue(
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
                UserRunOnceName,
                new RegValue(RegistryKind.ExpandString, cmdLine));
        }
    }
}
