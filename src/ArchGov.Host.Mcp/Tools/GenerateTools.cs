using System.ComponentModel;
using ArchGov.Core.Orchestration;
using ModelContextProtocol.Server;

namespace ArchGov.Host.Mcp.Tools;

[McpServerToolType]
public static class GenerateTools
{
    [McpServerTool(Name = "generate_component")]
    [Description("Generate a C# component (class, interface, record, etc.) that conforms to architecture rules. Returns the file path and generated source code.")]
    public static GenerateResult GenerateComponent(
        RuleResolutionHelper helper,
        IGenerateOrchestrator orchestrator,
        [Description("Component role (e.g. Entity, Repository, Controller, CommandHandler). Use get_component_roles to see available roles.")]
        string role,
        [Description("Component name (e.g. Order, OrderRepository, OrdersController).")]
        string name,
        [Description("Solution name (e.g. MyApp). Used for namespace generation.")]
        string solution,
        [Description("Feature name (e.g. Orders, Users). Used for namespace generation.")]
        string feature,
        [Description("Template name. Optional - auto-detects from .architecture/rules.yaml.")]
        string? template = null)
    {
        var resolved = helper.Resolve(template);

        var request = new GenerateRequest
        {
            SolutionName = solution,
            Feature = feature,
            Components =
            [
                new ComponentRequest { RoleName = role, Name = name },
            ],
        };

        return orchestrator.Generate(resolved, request);
    }
}
