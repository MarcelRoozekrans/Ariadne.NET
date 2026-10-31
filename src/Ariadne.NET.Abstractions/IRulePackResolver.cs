using ZeroAlloc.Results;

namespace Ariadne.NET.Abstractions;

public interface IRulePackResolver
{
    Result<RulePack, AriadneError> Resolve(RulePack userPack);

    Result<RulePack, AriadneError> Resolve(RulePack userPack, string? workingDirectory);
}
