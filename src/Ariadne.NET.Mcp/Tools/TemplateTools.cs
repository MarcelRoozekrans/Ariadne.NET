using System.ComponentModel;
using Ariadne.NET.Abstractions;
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
    [Description("Get the layers of the resolved pack with their id, selectors, allowed dependencies and because.")]
    public static IList<LayerDefinition> GetLayers(
        IRuleResolutionHelper helper,
        [Description("Template name (e.g. 'clean-architecture'). Optional - auto-detects from .architecture/rules.yaml if omitted.")]
        string? template = null)
    {
        var resolved = ToolError.ValueOrToolError(helper.Resolve(template));
        return resolved.Layers;
    }
}
