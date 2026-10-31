using ZeroAlloc.Results;

namespace Ariadne.NET.Abstractions;

public interface IBuiltinPackRegistry
{
    IReadOnlyList<string> GetAvailablePackNames();
    Result<RulePack, AriadneError> GetPack(string name);
}
