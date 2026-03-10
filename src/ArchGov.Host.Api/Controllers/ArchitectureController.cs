using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;
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

    public ArchitectureController(
        IBuiltinPackRegistry registry,
        IRulePackResolver resolver,
        IGenerateOrchestrator orchestrator)
    {
        _registry = registry;
        _resolver = resolver;
        _orchestrator = orchestrator;
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
}
