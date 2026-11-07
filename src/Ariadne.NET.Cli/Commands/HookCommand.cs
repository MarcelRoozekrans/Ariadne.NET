using System.CommandLine;
using System.Text;
using Ariadne.NET.Hooks;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Commands;

/// <summary>
/// <c>ariadne hook &lt;event&gt;</c>: the entry point Claude Code runs for its hooks. It fails open, so every outcome
/// exits 0; exit 2 would block the agent's tool call and any other non-zero code is reported as an error. Only a missing event argument is a usage error.
/// </summary>
public static class HookCommand
{
    // The hook protocol is UTF-8 JSON without a byte order mark, whatever the console code page is.
    private static readonly Encoding Protocol = new UTF8Encoding(false);

    public static Command Create(IServiceProvider provider) => Create(provider, OpenStandardStreams);

    internal static Command Create(IServiceProvider provider, Func<HookStreams> openStreams)
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
            try
            {
                var eventName = parseResult.GetValue(eventArgument)!;
                var host = provider.GetRequiredService<HookHost>();
                var streams = openStreams();
                using var stdin = new StreamReader(streams.Input, Protocol, true);
                using var stdout = new StreamWriter(streams.Output, Protocol) { AutoFlush = true };
                using var stderr = new StreamWriter(streams.Error, Protocol) { AutoFlush = true };
                return await host.RunAsync(eventName, stdin, stdout, stderr, cancellationToken).ConfigureAwait(false);
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

    private static HookStreams OpenStandardStreams() =>
        new(Console.OpenStandardInput(), Console.OpenStandardOutput(), Console.OpenStandardError());

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
