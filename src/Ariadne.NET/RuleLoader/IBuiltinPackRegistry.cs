using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.RuleLoader;

public interface IBuiltinPackRegistry
{
    IReadOnlyList<string> GetAvailablePackNames();
    RulePack GetPack(string name);
}
