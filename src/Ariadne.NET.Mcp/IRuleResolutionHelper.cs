using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Mcp;

public interface IRuleResolutionHelper
{
    Result<RulePack, AriadneError> Resolve(string? template);
}
