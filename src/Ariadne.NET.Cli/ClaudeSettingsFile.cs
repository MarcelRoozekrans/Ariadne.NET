using System.Text;
using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Cli;

/// <summary>
/// Reads, merges and replaces <c>.claude/settings.json</c> in two steps, so everything that can fail happens
/// before the caller writes anything else: <see cref="Prepare"/> leaves a finished temp file, <see cref="Commit"/> moves it in.
/// </summary>
internal static class ClaudeSettingsFile
{
    internal const string DisplayPath = ".claude/settings.json";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>The target to replace and the temp file holding the new text, or no temp file when nothing changes.</summary>
    internal sealed record Plan(string Target, string? TempPath);

    internal static Result<Plan, AriadneError> Prepare(string settingsPath)
    {
        if (Directory.Exists(settingsPath))
        {
            return Fail(AriadneErrorCode.Invalid, $"Cannot update {DisplayPath} because it is a directory.");
        }

        var target = settingsPath;
        string? existing = null;
        try
        {
            // A symlinked settings file is replaced at its target, so the link survives.
            var info = new FileInfo(settingsPath);
            target = info.LinkTarget is null ? settingsPath : info.ResolveLinkTarget(true)?.FullName ?? settingsPath;
            if (File.Exists(target))
            {
                existing = StrictUtf8.GetString(File.ReadAllBytes(target));
            }
        }
        catch (DecoderFallbackException)
        {
            return Fail(AriadneErrorCode.Invalid, $"Cannot update {DisplayPath} because it is not valid UTF-8 text.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(AriadneErrorCode.Unavailable, $"Cannot read {DisplayPath}.", ex.Message);
        }

        var merged = AgentSettings.Merge(existing);
        if (merged.IsFailure)
        {
            return Fail(merged.Error.Code, $"Cannot update {DisplayPath}. {merged.Error.Message}");
        }

        if (string.Equals(existing, merged.Value, StringComparison.Ordinal))
        {
            return Result<Plan, AriadneError>.Success(new Plan(target, null));
        }

        return WriteTemp(target, merged.Value);
    }

    /// <summary>Moves the prepared text over the target. <paramref name="rulesWritten"/> only shapes the error text.</summary>
    internal static Result<bool, AriadneError> Commit(Plan plan, bool rulesWritten)
    {
        if (plan.TempPath is null)
        {
            return Result<bool, AriadneError>.Success(true);
        }

        try
        {
            File.Move(plan.TempPath, plan.Target, overwrite: true);
            return Result<bool, AriadneError>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Discard(plan);
            var state = rulesWritten
                ? ".architecture/rules.yaml was written, but the hooks were not added."
                : "Nothing was written.";
            return Result<bool, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Unavailable, $"Cannot update {DisplayPath}. {state}", ex.Message));
        }
    }

    /// <summary>
    /// Removes the temp file when the caller gives up after <see cref="Prepare"/>. Best effort: a failure here must
    /// never replace the error that made the caller give up.
    /// </summary>
    internal static void Discard(Plan plan) => DeleteQuietly(plan.TempPath);

    private static void DeleteQuietly(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A stray temp file is the lesser problem; the caller reports the original failure.
        }
    }

    private static Result<Plan, AriadneError> WriteTemp(string target, string text)
    {
        var temp = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (!OperatingSystem.IsWindows() && File.Exists(target))
            {
                File.SetUnixFileMode(temp, File.GetUnixFileMode(target));
            }

            return Result<Plan, AriadneError>.Success(new Plan(target, temp));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(temp);
            return Fail(AriadneErrorCode.Unavailable, $"Cannot write {DisplayPath}. Nothing was written.", ex.Message);
        }
    }

    private static Result<Plan, AriadneError> Fail(AriadneErrorCode code, string message, string? detail = null) =>
        Result<Plan, AriadneError>.Failure(new AriadneError(code, message, detail));
}
