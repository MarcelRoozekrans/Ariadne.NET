using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

public interface IImportsLoader
{
    Result<ImportsManifest, AriadneError> LoadFromString(string yaml);
    Result<ImportsManifest, AriadneError> LoadFromFile(string filePath);
}
