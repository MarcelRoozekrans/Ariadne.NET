using Ariadne.NET.Cli;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAriadneNETServices();
using var provider = services.BuildServiceProvider();

return CliRootCommand.Invoke(provider, args);
