using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.Telemetry;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// Resolves a pack's <c>extends</c> references and merges them under it; each call opens an <c>ariadne.resolve</c> span,
/// with a child span for every pack it gets or loads through <paramref name="registry"/> and <paramref name="loader"/>.
/// </summary>
[Singleton]
public sealed class RulePackResolver(IBuiltinPackRegistry registry, IRulePackLoader loader, IRulePackMerger merger) : IRulePackResolver
{
    public Result<RulePack, AriadneError> Resolve(RulePack userPack) => Resolve(userPack, workingDirectory: null);

    public Result<RulePack, AriadneError> Resolve(RulePack userPack, string? workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(userPack);

        return PackOperations.Instrumented.Resolve(userPack, workingDirectory, registry, loader, merger);
    }
}
