using Ariadne.NET.Cli;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAriadneNETServices();
services.AddAriadneNETWorkspaces();
services.AddAriadneNETHooks();
using var provider = services.BuildServiceProvider();

// System.CommandLine cancels the action's token on Ctrl+C and SIGTERM by itself, so no handler is hooked here.
return await CliRootCommand.InvokeAsync(provider, args).ConfigureAwait(false);
