using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Loads a solution through <see cref="ISolutionLoader"/> and checks it with <see cref="IArchitectureValidator"/>.</summary>
public sealed class SolutionValidator : ISolutionValidator
{
    private readonly ISolutionLoader _loader;
    private readonly IArchitectureValidator _validator;

    /// <summary>Creates the validator.</summary>
    /// <param name="loader">Loads the solution.</param>
    /// <param name="validator">Checks the loaded projects.</param>
    public SolutionValidator(ISolutionLoader loader, IArchitectureValidator validator)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);

        _loader = loader;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<Result<ArchitectureReport, AriadneError>> ValidateAsync(
        RulePack resolvedPack,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(path);

        var loaded = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result<ArchitectureReport, AriadneError>.Failure(loaded.Error);
        }

        using var solution = loaded.Value;

        // A project without a package list has a missing, stale or unmatched assets file: the user fixes that by restoring,
        // so it is reported as an error here instead of reaching the validator, which throws on it.
        if (_validator.FindPackageCoverageGap(resolvedPack, solution.Projects) is { } uncovered)
        {
            return Result<ArchitectureReport, AriadneError>.Failure(UnknownPackages(uncovered));
        }

        var report = _validator.Validate(resolvedPack, solution.Projects, cancellationToken);
        var named = report.Violations.Select(violation => violation with { Pack = resolvedPack.Name }).ToList();
        return Result<ArchitectureReport, AriadneError>.Success(report with { Violations = named, SkippedProjects = solution.SkippedProjects });
    }

    private static AriadneError UnknownPackages(PackageCoverageGap uncovered) =>
        new(
            AriadneErrorCode.Invalid,
            $"Rule '{uncovered.RuleId}' bans packages, but project '{uncovered.ProjectName}' has no usable package list: its obj/project.assets.json is missing, stale or does not match its target framework. Run 'dotnet restore' and validate again.");
}
