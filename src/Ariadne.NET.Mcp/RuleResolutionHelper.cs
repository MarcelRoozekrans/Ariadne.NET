using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.Mcp;

[Singleton]
public sealed class RuleResolutionHelper : IRuleResolutionHelper
{
    private readonly IBuiltinPackRegistry _registry;
    private readonly IRulePackResolver _resolver;
    private readonly IRulePackLoader _loader;

    public RuleResolutionHelper(
        IBuiltinPackRegistry registry,
        IRulePackResolver resolver,
        IRulePackLoader loader)
    {
        _registry = registry;
        _resolver = resolver;
        _loader = loader;
    }

    public Result<RulePack, AriadneError> Resolve(string? template)
    {
        // Every path passes the working directory, so an import:// reference resolves from the local cache.
        var workingDirectory = Directory.GetCurrentDirectory();

        if (template is not null)
        {
            var extends = TemplateReference.ToExtends(template);
            if (extends.IsFailure)
            {
                return Result<RulePack, AriadneError>.Failure(extends.Error);
            }

            var userPack = new RulePack
            {
                Name = template,
                Extends = extends.Value,
            };
            return _resolver.Resolve(userPack, workingDirectory);
        }

        var rulesPath = Path.Combine(workingDirectory, ".architecture", "rules.yaml");

        if (File.Exists(rulesPath))
        {
            var pack = _loader.LoadFromFile(rulesPath);
            if (pack.IsFailure)
            {
                return pack;
            }

            return _resolver.Resolve(pack.Value, workingDirectory);
        }

        var defaultPack = new RulePack
        {
            Name = "clean-architecture",
            Extends = ["builtin://clean-architecture"],
        };
        return _resolver.Resolve(defaultPack, workingDirectory);
    }
}
