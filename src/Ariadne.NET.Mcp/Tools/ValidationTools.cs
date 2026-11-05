using System.ComponentModel;
using Ariadne.NET.Analysis;
using Ariadne.NET.Workspaces;
using ModelContextProtocol.Server;

namespace Ariadne.NET.Mcp.Tools;

[McpServerToolType]
public static class ValidationTools
{
    [McpServerTool(Name = "validate_source")]
    [Description("Validate a solution or project against the architecture rules, using the semantic type graph of its compilations. Reports each layer dependency that is not allowed and each component role mismatch.")]
    public static async Task<ArchitectureReport> ValidateSource(
        IRuleResolutionHelper helper,
        ISolutionValidator validator,
        [Description("Path to a .sln, .slnx, .slnf or .csproj, or a directory holding exactly one.")]
        string path,
        [Description("Template name. Optional - auto-detects from .architecture/rules.yaml.")]
        string? template = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = ToolError.ValueOrToolError(helper.Resolve(template));
        var result = await validator.ValidateAsync(resolved, path, cancellationToken).ConfigureAwait(false);
        return ToolError.ValueOrToolError(result);
    }
}
