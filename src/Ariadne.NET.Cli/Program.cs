using Ariadne.NET.Cli;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAriadneNETServices();
services.AddAriadneNETWorkspaces();
using var provider = services.BuildServiceProvider();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

return await CliRootCommand.InvokeAsync(provider, args, cancellation.Token).ConfigureAwait(false);
