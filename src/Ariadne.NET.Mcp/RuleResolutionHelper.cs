using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;

using ZeroAlloc.Inject;

namespace Ariadne.NET.Mcp;

[Singleton]
public sealed class RuleResolutionHelper
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

    public RulePack Resolve(string? template)
    {
        if (template is not null)
        {
            var userPack = new RulePack
            {
                Name = template,
                Extends = TemplateReference.ToExtends(template),
            };
            return _resolver.Resolve(userPack);
        }

        var rulesPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            ".architecture",
            "rules.yaml");

        if (File.Exists(rulesPath))
        {
            var pack = _loader.LoadFromFile(rulesPath);
            return _resolver.Resolve(pack);
        }

        var defaultPack = new RulePack
        {
            Name = "clean-architecture",
            Extends = ["builtin://clean-architecture"],
        };
        return _resolver.Resolve(defaultPack);
    }
}
