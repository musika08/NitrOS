using System.Security;

namespace NitrOS.Live.Launch;

public static class AfterUpdateTaskXml
{
    public static string Build(string exePath, string userSid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);

        string escapedPath = SecurityElement.Escape(exePath);
        string escapedSid = SecurityElement.Escape(userSid);

        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{escapedSid}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{escapedSid}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT10M</ExecutionTimeLimit>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{escapedPath}</Command>
      <Arguments>--after-update</Arguments>
    </Exec>
  </Actions>
</Task>";
    }
}
