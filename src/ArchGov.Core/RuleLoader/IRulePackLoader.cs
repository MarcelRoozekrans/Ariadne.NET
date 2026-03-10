using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleLoader;

public interface IRulePackLoader
{
    RulePack LoadFromString(string yaml);
    RulePack LoadFromFile(string filePath);
}
