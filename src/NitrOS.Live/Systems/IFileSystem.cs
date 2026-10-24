namespace NitrOS.Live.Systems;

public interface IFileSystem
{
    bool Exists(string path);
    void TakeOwnershipAndDelete(string path);
}
