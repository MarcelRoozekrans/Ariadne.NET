using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddArchGovCoreServices();
builder.Services.AddArchGovHostMcpServices();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(ArchGov.Host.Mcp.Tools.TemplateTools).Assembly)
    .WithResourcesFromAssembly(typeof(ArchGov.Host.Mcp.Resources.PackResources).Assembly);

var app = builder.Build();

app.MapMcp();

app.Run();

namespace ArchGov.Host.Api
{
    public partial class Program;
}
