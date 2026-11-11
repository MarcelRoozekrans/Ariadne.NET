using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Validates a solution against every pack file under <c>.architecture</c>, loading the solution once.</summary>
internal sealed class PackSetValidator : IPackSetValidator
{
    private readonly ISolutionLoader _loader;
    private readonly IArchitectureValidator _validator;
    private readonly IRulePackLoader _packLoader;
    private readonly IRulePackResolver _resolver;

    /// <summary>Creates the validator.</summary>
    /// <param name="loader">Loads the solution.</param>
    /// <param name="validator">Checks the loaded projects.</param>
    /// <param name="packLoader">Reads each pack file.</param>
    /// <param name="resolver">Resolves each pack's <c>extends</c> references.</param>
    public PackSetValidator(ISolutionLoader loader, IArchitectureValidator validator, IRulePackLoader packLoader, IRulePackResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(packLoader);
        ArgumentNullException.ThrowIfNull(resolver);

        _loader = loader;
        _validator = validator;
        _packLoader = packLoader;
        _resolver = resolver;
    }

    /// <inheritdoc />
    public async Task<Result<ArchitectureReport, AriadneError>> ValidateAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var discovered = PackDiscovery.Find(path);
        if (discovered.IsFailure)
        {
            return Result<ArchitectureReport, AriadneError>.Failure(discovered.Error);
        }

        var packs = ResolveAll(discovered.Value);
        if (packs.IsFailure)
        {
            return Result<ArchitectureReport, AriadneError>.Failure(packs.Error);
        }

        var loaded = await _loader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result<ArchitectureReport, AriadneError>.Failure(loaded.Error);
        }

        using var solution = loaded.Value;

        ArchitectureReport? merged = null;
        var violations = new List<ArchitectureViolation>();
        foreach (var pack in packs.Value)
        {
            var checkedPack = SolutionValidator.Check(_validator, solution, pack, cancellationToken);
            if (checkedPack.IsFailure)
            {
                return checkedPack;
            }

            merged ??= checkedPack.Value;
            violations.AddRange(checkedPack.Value.Violations);
        }

        if (merged is null)
        {
            return Result<ArchitectureReport, AriadneError>.Failure(
                new AriadneError(AriadneErrorCode.NotFound, "No rule pack was found to check the solution against."));
        }

        return Result<ArchitectureReport, AriadneError>.Success(merged with { Violations = violations });
    }

    private Result<IReadOnlyList<RulePack>, AriadneError> ResolveAll(DiscoveredPacks discovered)
    {
        var packs = new List<RulePack>();
        var fileByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in discovered.Files)
        {
            var fileName = Path.GetFileName(file);
            var loaded = _packLoader.LoadFromFile(file);
            var resolved = loaded.IsSuccess ? _resolver.Resolve(loaded.Value, discovered.ProjectRoot) : loaded;
            if (resolved.IsFailure)
            {
                // A pack inside a found .architecture folder that cannot be loaded or resolved is a broken pack, never a missing
                // one: NotFound from the pack set only means discovery found nothing or the path does not exist.
                return Failure(resolved.Error with { Code = AriadneErrorCode.Invalid, Message = Named(fileName, resolved.Error.Message) });
            }

            var pack = resolved.Value;
            if (fileByName.TryGetValue(pack.Name, out var first))
            {
                return Failure(new AriadneError(
                    AriadneErrorCode.Invalid,
                    $"Pack files '{first}' and '{fileName}' both declare the pack name '{pack.Name}'; give each pack its own name."));
            }

            fileByName[pack.Name] = fileName;
            packs.Add(pack);
        }

        return Result<IReadOnlyList<RulePack>, AriadneError>.Success(packs);
    }

    // Loader messages for a bad file already name it; the others (an unknown extends target, say) do not.
    private static string Named(string fileName, string message) =>
        message.Contains(fileName, StringComparison.OrdinalIgnoreCase) ? message : $"In pack file '{fileName}': {message}";

    private static Result<IReadOnlyList<RulePack>, AriadneError> Failure(AriadneError error) =>
        Result<IReadOnlyList<RulePack>, AriadneError>.Failure(error);
}
