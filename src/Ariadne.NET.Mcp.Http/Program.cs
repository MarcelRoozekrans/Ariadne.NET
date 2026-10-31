using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAriadneNETServices();
builder.Services.AddAriadneNETMcpServices();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(Ariadne.NET.Mcp.Tools.TemplateTools).Assembly)
    .WithResourcesFromAssembly(typeof(Ariadne.NET.Mcp.Resources.PackResources).Assembly);

var app = builder.Build();

app.MapMcp();

app.Run();

namespace Ariadne.NET.Mcp.Http
{
    public partial class Program;
}
