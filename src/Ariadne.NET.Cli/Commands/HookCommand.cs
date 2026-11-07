using System.CommandLine;
using Ariadne.NET.Hooks;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Commands;

/// <summary>
/// <c>ariadne hook &lt;event&gt;</c>: the entry point Claude Code runs for its hooks. It fails open, so every outcome
/// exits 0; a non-zero exit would block the agent's tool call. Only a missing event argument is a usage error.
/// </summary>
public static class HookCommand
{
    public static Command Create(IServiceProvider provider)
    {
        var eventArgument = new Argument<string>("event")
        {
            Description = "The hook event: session-start, pre-tool-use or stop",
        };

        var command = new Command("hook", "Run an agent hook for Claude Code; reads the hook input on stdin")
        {
            eventArgument,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var eventName = parseResult.GetValue(eventArgument)!;
            try
            {
                var host = provider.GetRequiredService<HookHost>();
                return await host.RunAsync(eventName, Console.In, Console.Out, Console.Error, cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // A hook must never fail the agent: nothing may escape the action.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ReportUnavailable(ex);
                return ExitCodes.Success;
            }
        });

        return command;
    }

    private static void ReportUnavailable(Exception ex)
    {
        try
        {
            Console.Error.WriteLine($"ariadne hook: the hook host is unavailable ({ex.GetType().Name}).");
        }
        catch (Exception writeError) when (writeError is IOException or ObjectDisposedException)
        {
            // stderr is gone; there is nowhere left to report to, and the exit code must stay 0.
        }
    }
}
