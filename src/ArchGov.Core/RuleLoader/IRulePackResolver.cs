using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleLoader;

public interface IRulePackResolver
{
    RulePack Resolve(RulePack userPack);

    RulePack Resolve(RulePack userPack, string? workingDirectory);
}
