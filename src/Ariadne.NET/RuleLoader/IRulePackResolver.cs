using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.RuleLoader;

public interface IRulePackResolver
{
    RulePack Resolve(RulePack userPack);

    RulePack Resolve(RulePack userPack, string? workingDirectory);
}
