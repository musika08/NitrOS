namespace NitrOS.App.Logic.Navigation;

public interface IScreen
{
    ScreenKey Key { get; }
    void OnShown(object? argument);
}
