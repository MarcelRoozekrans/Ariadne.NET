using System.ComponentModel;
using Ariadne.NET.Abstractions;
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
        IPackSetValidator packSetValidator,
        [Description("Path to a .sln, .slnx, .slnf or .csproj, or a directory holding exactly one.")]
        string path,
        [Description("Template name. Optional. Without it every pack file in the nearest .architecture folder is checked; with no such folder the rules.yaml in the current directory or the clean-architecture default applies.")]
        string? template = null,
        CancellationToken cancellationToken = default)
    {
        if (template is null)
        {
            var set = await packSetValidator.ValidateAsync(path, cancellationToken).ConfigureAwait(false);
            if (set.IsSuccess || set.Error.Code != AriadneErrorCode.NotFound)
            {
                return ToolError.ValueOrToolError(set);
            }
        }

        var resolved = ToolError.ValueOrToolError(helper.Resolve(template));
        var result = await validator.ValidateAsync(resolved, path, cancellationToken).ConfigureAwait(false);
        return ToolError.ValueOrToolError(result);
    }
}
