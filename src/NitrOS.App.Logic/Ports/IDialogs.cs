namespace NitrOS.App.Logic.Ports;

public sealed record ConfirmRequest(
    string Title,
    string Message,
    IReadOnlyList<string> Items,
    string ConfirmText,
    string CancelText);

public interface IDialogs
{
    Task<bool> ConfirmAsync(ConfirmRequest request);
    Task ShowAsync(string title, string message);
}
