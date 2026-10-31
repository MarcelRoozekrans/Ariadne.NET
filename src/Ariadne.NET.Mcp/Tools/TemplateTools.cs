using System.ComponentModel;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.RuleSchema;
using ModelContextProtocol.Server;

namespace Ariadne.NET.Mcp.Tools;

[McpServerToolType]
public static class TemplateTools
{
    [McpServerTool(Name = "list_templates")]
    [Description("List all available architecture templates (e.g. clean-architecture, hexagonal-architecture, cqrs).")]
    public static IReadOnlyList<string> ListTemplates(IBuiltinPackRegistry registry)
    {
        return registry.GetAvailablePackNames();
    }

    [McpServerTool(Name = "get_layers")]
    [Description("Get architecture layer definitions with allowed dependencies. If no template specified, uses .architecture/rules.yaml or defaults to clean-architecture.")]
    public static IList<LayerDefinition> GetLayers(
        RuleResolutionHelper helper,
        [Description("Template name (e.g. 'clean-architecture'). Optional - auto-detects from .architecture/rules.yaml if omitted.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return resolved.Layers;
    }
}
