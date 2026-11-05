using System.IO.Pipelines;
using Ariadne.NET.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Ariadne.NET.Mcp.Tests;

/// <summary>
/// Runs the real MCP server with the Ariadne tools over in-memory pipes and connects a client to it,
/// so tests see a tool call exactly as an MCP client does.
/// </summary>
internal sealed class McpClientHarness : IAsyncDisposable
{
    private readonly IHost _host;

    private McpClientHarness(IHost host, McpClient client)
    {
        _host = host;
        Client = client;
    }

    public McpClient Client { get; }

    public static async Task<McpClientHarness> StartAsync()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Services.AddAriadneNETServices();
        builder.Services.AddAriadneNETWorkspaces();
        builder.Services.AddAriadneNETMcpServices();
        builder.Services
            .AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithToolsFromAssembly(typeof(RuleTools).Assembly);

        var host = builder.Build();
        await host.StartAsync().ConfigureAwait(false);

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()))
            .ConfigureAwait(false);

        return new McpClientHarness(host, client);
    }

    public Task<CallToolResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> arguments) =>
        Client.CallToolAsync(toolName, arguments).AsTask();

    public static string TextOf(CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync().ConfigureAwait(false);
        await _host.StopAsync().ConfigureAwait(false);
        _host.Dispose();
    }
}
