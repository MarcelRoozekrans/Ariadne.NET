using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleLoader;

public interface IBuiltinPackRegistry
{
    IReadOnlyList<string> GetAvailablePackNames();
    RulePack GetPack(string name);
}
