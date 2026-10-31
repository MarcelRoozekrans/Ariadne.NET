using Ariadne.NET.Abstractions;
using Ariadne.NET.Telemetry;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.RuleLoader;

/// <summary>Loads rule packs from YAML; each call opens an <c>ariadne.load</c> span.</summary>
[Singleton]
public sealed class YamlRulePackLoader : IRulePackLoader
{
    public Result<RulePack, AriadneError> LoadFromString(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        return PackOperations.Instrumented.LoadFromString(yaml);
    }

    public Result<RulePack, AriadneError> LoadFromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        return PackOperations.Instrumented.LoadFromFile(filePath);
    }
}
