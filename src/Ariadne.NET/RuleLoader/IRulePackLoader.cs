using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.RuleLoader;

public interface IRulePackLoader
{
    RulePack LoadFromString(string yaml);
    RulePack LoadFromFile(string filePath);
}
