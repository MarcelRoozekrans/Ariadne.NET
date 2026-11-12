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
        IRuleResolutionHelper helper,
        [Description("Template name. Optional - auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = ToolError.ValueOrToolError(helper.Resolve(template));
        return resolved.ComponentRoles;
    }

    [McpServerTool(Name = "get_rules")]
    [Description("Get the forbid, allowOnly, require, bannedPackages, uniqueNames and slices rules of the resolved pack, each with its id, targets, requirement, packages, against selector, slices, exceptions and because. A forbid or allowOnly target may select members.")]
    public static IList<RuleDefinition> GetRules(
        IRuleResolutionHelper helper,
        [Description("Template name. Optional - auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = ToolError.ValueOrToolError(helper.Resolve(template));
        return resolved.Rules;
    }
}
