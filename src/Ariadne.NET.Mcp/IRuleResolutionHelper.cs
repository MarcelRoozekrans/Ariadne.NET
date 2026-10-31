using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Mcp;

public interface IRuleResolutionHelper
{
    RulePack Resolve(string? template);
}
