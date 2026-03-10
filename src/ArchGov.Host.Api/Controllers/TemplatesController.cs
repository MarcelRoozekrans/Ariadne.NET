using ArchGov.Core.RuleLoader;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace ArchGov.Host.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/architecture/templates")]
public class TemplatesController : ControllerBase
{
    private readonly IBuiltinPackRegistry _registry;

    public TemplatesController(IBuiltinPackRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public IActionResult GetTemplates()
    {
        var names = _registry.GetAvailablePackNames();
        return Ok(names);
    }
}
