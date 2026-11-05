namespace Ariadne.NET.Cli;

/// <summary>
/// Process exit codes, so CI pipelines can tell rule violations apart from failures to run.
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;

    /// <summary>The command ran and found architecture violations.</summary>
    public const int Violations = 1;

    /// <summary>The command could not do its job: bad input, missing files, failed generation or sync.</summary>
    public const int Error = 2;
}
