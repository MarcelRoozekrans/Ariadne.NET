using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;
using ArchGov.Core.Validation;
using ArchGov.Host.Api.Models;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace ArchGov.Host.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/architecture")]
public class ArchitectureController : ControllerBase
{
    private readonly IBuiltinPackRegistry _registry;
    private readonly IRulePackResolver _resolver;
    private readonly IGenerateOrchestrator _orchestrator;
    private readonly ISourceValidator _validator;
    private readonly ILinter _linter;
    private readonly IWorkspaceOrchestrator _workspaceOrchestrator;

    public ArchitectureController(
        IBuiltinPackRegistry registry,
        IRulePackResolver resolver,
        IGenerateOrchestrator orchestrator,
        ISourceValidator validator,
        ILinter linter,
        IWorkspaceOrchestrator workspaceOrchestrator)
    {
        _registry = registry;
        _resolver = resolver;
        _orchestrator = orchestrator;
        _validator = validator;
        _linter = linter;
        _workspaceOrchestrator = workspaceOrchestrator;
    }

    [HttpGet("layers")]
    public IActionResult GetLayers([FromQuery] string template = "clean-architecture")
    {
        var pack = _registry.GetPack(template);
        return Ok(pack.Layers);
    }

    [HttpGet("rules")]
    public IActionResult GetRules([FromQuery] string template = "clean-architecture")
    {
        var pack = _registry.GetPack(template);
        return Ok(pack.CodeFeatureRules);
    }

    [HttpGet("component-roles")]
    public IActionResult GetComponentRoles([FromQuery] string template = "clean-architecture")
    {
        var pack = _registry.GetPack(template);
        return Ok(pack.ComponentRoles);
    }

    [HttpPost("generate")]
    public IActionResult Generate([FromBody] ApiGenerateRequest request)
    {
        var template = request.Template ?? "clean-architecture";

        var userPack = new RulePack
        {
            Name = request.SolutionName,
            Extends = [$"builtin://{template}"],
        };
        var resolved = _resolver.Resolve(userPack);

        var generateRequest = new GenerateRequest
        {
            SolutionName = request.SolutionName,
            Feature = request.Features?.FirstOrDefault() ?? "Default",
            Components = request.Components.Select(static c => new ComponentRequest
            {
                RoleName = c.ComponentRole,
                Name = c.Name,
            }).ToList(),
        };

        var result = _orchestrator.Generate(resolved, generateRequest);
        return Ok(result);
    }

    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ApiValidateRequest request)
    {
        var template = request.Template ?? "clean-architecture";
        var userPack = new RulePack
        {
            Name = "validation",
            Extends = [$"builtin://{template}"],
        };
        var resolved = _resolver.Resolve(userPack);
        var result = _validator.Validate(resolved, request.SourceDirectory);
        return Ok(result);
    }

    [HttpPost("lint")]
    public IActionResult Lint([FromBody] ApiValidateRequest request)
    {
        var template = request.Template ?? "clean-architecture";
        var userPack = new RulePack
        {
            Name = "lint",
            Extends = [$"builtin://{template}"],
        };
        var resolved = _resolver.Resolve(userPack);
        var result = _linter.Lint(resolved, request.SourceDirectory);
        return Ok(result);
    }

    [HttpPost("generate-workspace")]
    public IActionResult GenerateWorkspace([FromBody] ApiWorkspaceRequest request)
    {
        var template = request.Template ?? "clean-architecture";
        var userPack = new RulePack
        {
            Name = request.SolutionName,
            Extends = [$"builtin://{template}"],
        };
        var resolved = _resolver.Resolve(userPack);

        var workspaceRequest = new WorkspaceGenerateRequest
        {
            SolutionName = request.SolutionName,
            Features = request.Features,
        };

        var result = _workspaceOrchestrator.GenerateWorkspace(resolved, workspaceRequest);
        return Ok(result);
    }
}
