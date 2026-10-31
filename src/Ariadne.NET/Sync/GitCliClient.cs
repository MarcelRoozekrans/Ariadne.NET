using System.Diagnostics;
using System.Globalization;

using ZeroAlloc.Resilience;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

/// <summary>
/// Runs git as a child process. A non-zero exit throws, so the generated resilience proxy retries it
/// and reports the last failure as a <see cref="ResilienceError"/>.
/// </summary>
internal sealed class GitCliClient : IGitClient
{
    public async ValueTask<Result<string, ResilienceError>> RunAsync(string arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // A missing git binary throws Win32Exception here; the proxy reports it like any other failure.
        process.Start();

        try
        {
            // Read both streams at once: git blocks when the pipe it is not being read from fills up.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "git {0} failed with exit code {1}: {2}",
                        arguments,
                        process.ExitCode,
                        stderr.Trim()));
            }

            return Result<string, ResilienceError>.Success(stdout);
        }
        catch (OperationCanceledException)
        {
            // Cancellation or the policy timeout: do not leave git running behind us.
            process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
