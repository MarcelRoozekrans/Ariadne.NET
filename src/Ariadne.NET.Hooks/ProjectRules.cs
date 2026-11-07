using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using ZeroAlloc.Results;

namespace Ariadne.NET.Hooks;

/// <inheritdoc />
public sealed class ProjectRules(IRulePackLoader loader, IRulePackResolver resolver) : IProjectRules
{
    /// <inheritdoc />
    public Result<RulePack, AriadneError> Load(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var rulesPath = Path.Combine(root, ".architecture", "rules.yaml");
        if (!File.Exists(rulesPath))
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                "No .architecture/rules.yaml found. Run 'ariadne init' first.",
                rulesPath));
        }

        var userPack = loader.LoadFromFile(rulesPath);
        return userPack.IsFailure ? userPack : resolver.Resolve(userPack.Value, root);
    }
}
