using NitrOS.App.Logic.Ports;

namespace NitrOS.App.Tests.Fakes;

public sealed class FakeDialogs : IDialogs
{
    public List<ConfirmRequest> ConfirmRequests { get; } = [];
    public List<(string Title, string Message)> ShowRequests { get; } = [];
    public bool NextConfirmResult { get; set; } = true;

    public Task<bool> ConfirmAsync(ConfirmRequest request)
    {
        ConfirmRequests.Add(request);
        return Task.FromResult(NextConfirmResult);
    }

    public Task ShowAsync(string title, string message)
    {
        ShowRequests.Add((title, message));
        return Task.CompletedTask;
    }
}
