namespace NitrOS.App.Logic.Ports;

using System.Threading.Tasks;

public interface IFilePicker
{
    Task<string?> PickIsoToOpenAsync();
    Task<string?> PickIsoToSaveAsync(string suggestedName);
}
