namespace ArchGov.Core.Sync;

public interface IImportsLoader
{
    ImportsManifest LoadFromString(string yaml);
    ImportsManifest LoadFromFile(string filePath);
}
