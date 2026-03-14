using System.CommandLine;
using System.CommandLine.Parsing;
using ArchGov.Host.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddArchGovCoreServices();
using var provider = services.BuildServiceProvider();

var rootCommand = new RootCommand("Architecture Governance Engine CLI");

rootCommand.Subcommands.Add(InitCommand.Create(provider));
rootCommand.Subcommands.Add(RulesCommand.Create(provider));
rootCommand.Subcommands.Add(RolesCommand.Create(provider));
rootCommand.Subcommands.Add(GenerateCommand.Create(provider));
rootCommand.Subcommands.Add(ValidateCommand.Create(provider));
rootCommand.Subcommands.Add(LintCommand.Create(provider));
rootCommand.Subcommands.Add(SyncCommand.Create(provider));

var parseResult = CommandLineParser.Parse(rootCommand, args, new ParserConfiguration());
return parseResult.Invoke();
