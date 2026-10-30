using ArchGov.Host.Cli;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddArchGovCoreServices();
using var provider = services.BuildServiceProvider();

return CliRootCommand.Invoke(provider, args);
