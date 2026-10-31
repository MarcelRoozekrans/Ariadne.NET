using ZeroAlloc.Results;

namespace Ariadne.NET.Abstractions;

public interface IRulePackLoader
{
    Result<RulePack, AriadneError> LoadFromString(string yaml);
    Result<RulePack, AriadneError> LoadFromFile(string filePath);
}
