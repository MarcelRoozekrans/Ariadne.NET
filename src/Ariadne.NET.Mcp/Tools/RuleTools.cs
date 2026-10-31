using System.ComponentModel;
using Ariadne.NET.Abstractions;
using ModelContextProtocol.Server;

namespace Ariadne.NET.Mcp.Tools;

[McpServerToolType]
public static class RuleTools
{
    [McpServerTool(Name = "get_component_roles")]
    [Description("Get available component roles (Entity, Repository, Controller, etc.) with their layer, type kind, and accessibility. Use this to know what roles are valid for generate_component.")]
    public static IList<ComponentRoleDefinition> GetComponentRoles(
        RuleResolutionHelper helper,
        [Description("Template name. Optional - auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return resolved.ComponentRoles;
    }

    [McpServerTool(Name = "get_rules")]
    [Description("Get code feature rules that enforce structural constraints (e.g. interface requirements, accessibility rules).")]
    public static IList<CodeFeatureRuleDefinition> GetRules(
        RuleResolutionHelper helper,
        [Description("Template name. Optional - auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return resolved.CodeFeatureRules;
    }
}
