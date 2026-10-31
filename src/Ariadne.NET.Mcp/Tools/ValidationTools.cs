using System.ComponentModel;
using Ariadne.NET.Validation;
using ModelContextProtocol.Server;

namespace Ariadne.NET.Mcp.Tools;

[McpServerToolType]
public static class ValidationTools
{
    [McpServerTool(Name = "validate_source")]
    [Description("Validate existing source files against architecture dependency rules. Checks that using directives comply with layer dependency constraints (e.g. Domain cannot reference Infrastructure).")]
    public static SourceValidationResult ValidateSource(
        IRuleResolutionHelper helper,
        ISourceValidator validator,
        [Description("Absolute or relative path to the source directory to validate.")]
        string sourceDirectory,
        [Description("Template name. Optional - auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return validator.Validate(resolved, sourceDirectory);
    }

    [McpServerTool(Name = "lint_source")]
    [Description("Lint source files for namespace-vs-directory layer mismatches. Lighter than validate - checks that namespace segments match the directory-based layer structure.")]
    public static LintResult LintSource(
        IRuleResolutionHelper helper,
        ILinter linter,
        [Description("Absolute or relative path to the source directory to lint.")]
        string sourceDirectory,
        [Description("Template name. Optional - auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        return linter.Lint(resolved, sourceDirectory);
    }
}
