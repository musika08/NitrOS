using NitrOS.Cli;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Finish the current action and roll back that switch instead of dying half-way.
    e.Cancel = true;
    cts.Cancel();
};

return CliApp.Run(args, Console.Out, CliEnvironment.Real(), cts.Token);
