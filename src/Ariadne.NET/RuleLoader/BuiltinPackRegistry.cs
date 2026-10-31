using Ariadne.NET.Abstractions;
using Ariadne.NET.Telemetry;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.RuleLoader;

/// <summary>Serves the embedded built-in packs; each <see cref="GetPack"/> call opens an <c>ariadne.get_pack</c> span.</summary>
[Singleton]
public sealed class BuiltinPackRegistry : IBuiltinPackRegistry
{
    private readonly BuiltinPackCatalog _catalog;

    public BuiltinPackRegistry(IRulePackLoader loader)
    {
        _catalog = new BuiltinPackCatalog(loader);
    }

    public IReadOnlyList<string> GetAvailablePackNames() => BuiltinPackCatalog.Names;

    public Result<RulePack, AriadneError> GetPack(string name) => PackOperations.Instrumented.GetPack(name, _catalog);
}
