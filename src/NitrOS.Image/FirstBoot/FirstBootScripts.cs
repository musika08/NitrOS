namespace NitrOS.Image.FirstBoot;

using System;
using System.Collections.Generic;
using System.Text;

public sealed record RenderedScripts(
    string? MachineCmd,
    string? UserCmd,
    IReadOnlyList<(string FileName, string Body)> ScriptFiles,
    IReadOnlyList<(string SwitchId, string Detail)> Refused);

public static class FirstBootScripts
{
    public static RenderedScripts Render(FirstBootPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var refused = new List<(string SwitchId, string Detail)>();
        var scriptFiles = new List<(string FileName, string Body)>();

        string? machineCmd = null;
        if (plan.Machine.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal");
            sb.AppendLine("set \"N=%SystemRoot%\\Setup\\Scripts\\NitrOS\"");
            sb.AppendLine("if exist \"%N%\\machine.done\" exit /b 0");
            sb.AppendLine("echo route %1 >> \"%N%\\machine.log\"");

            var scriptIndex = 0;
            var commandIndex = 0;

            foreach (var item in plan.Machine)
            {
                try
                {
                    switch (item)
                    {
                        case MachineRegSet rs:
                            var addCmd = CmdText.RegAdd(rs.Key, rs.Name, rs.Value);
                            sb.AppendLine(addCmd);
                            sb.AppendLine($"echo {rs.SwitchId} {commandIndex++} exit %errorlevel% >> \"%N%\\machine.log\"");
                            break;

                        case MachineRegDelete rd:
                            var delCmd = CmdText.RegDelete(rd.Key, rd.Name);
                            sb.AppendLine(delCmd);
                            sb.AppendLine($"echo {rd.SwitchId} {commandIndex++} exit %errorlevel% >> \"%N%\\machine.log\"");
                            break;

                        case TaskOff to:
                            var taskCmd = CmdText.TaskOff(to.Path);
                            sb.AppendLine(taskCmd);
                            sb.AppendLine($"echo {to.SwitchId} {commandIndex++} exit %errorlevel% >> \"%N%\\machine.log\"");
                            break;

                        case PowerValue pv:
                            var powerCmd = CmdText.Power(pv.Subgroup, pv.Setting, pv.Ac, pv.Dc);
                            sb.AppendLine(powerCmd);
                            sb.AppendLine($"echo {pv.SwitchId} {commandIndex++} exit %errorlevel% >> \"%N%\\machine.log\"");
                            break;

                        case ScriptRun sr:
                            var fileName = $"script-{scriptIndex++}.ps1";
                            scriptFiles.Add((fileName, sr.Body));
                            sb.AppendLine($"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%N%\\{fileName}\"");
                            sb.AppendLine($"echo {sr.SwitchId} {commandIndex++} exit %errorlevel% >> \"%N%\\machine.log\"");
                            break;
                    }
                }
                catch (UnsafeTextException)
                {
                    refused.Add((item.SwitchId, "cannot be written safely at first start"));
                }
            }

            sb.AppendLine("echo done > \"%N%\\machine.done\"");
            machineCmd = sb.ToString();
        }

        string? userCmd = null;
        if (plan.User.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal");
            sb.AppendLine("set \"L=%LOCALAPPDATA%\\NitrOS\"");
            sb.AppendLine("if not exist \"%L%\" mkdir \"%L%\"");

            var commandIndex = 0;

            foreach (var item in plan.User)
            {
                try
                {
                    switch (item)
                    {
                        case UserRegSet urs:
                            var addCmd = CmdText.RegAdd(urs.Key, urs.Name, urs.Value);
                            sb.AppendLine(addCmd);
                            sb.AppendLine($"echo {urs.SwitchId} {commandIndex++} exit %errorlevel% >> \"%L%\\firstlogon.log\"");
                            break;

                        case UserRegDelete urd:
                            var delCmd = CmdText.RegDelete(urd.Key, urd.Name);
                            sb.AppendLine(delCmd);
                            sb.AppendLine($"echo {urd.SwitchId} {commandIndex++} exit %errorlevel% >> \"%L%\\firstlogon.log\"");
                            break;
                    }
                }
                catch (UnsafeTextException)
                {
                    refused.Add((item.SwitchId, "cannot be written safely at first start"));
                }
            }

            userCmd = sb.ToString();
        }

        return new RenderedScripts(machineCmd, userCmd, scriptFiles, refused);
    }
}
