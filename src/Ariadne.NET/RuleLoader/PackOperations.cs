using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.Telemetry;

using ZeroAlloc.Results;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// The one implementation of the pack operations. <see cref="Instrumented"/> wraps it in the generated proxy; the public
/// <see cref="YamlRulePackLoader"/>, <see cref="BuiltinPackRegistry"/> and <see cref="RulePackResolver"/> call that proxy
/// and pass their own collaborators, so a resolve opens child spans for the packs it gets and loads through them.
/// </summary>
internal sealed class PackOperations : IPackOperations
{
    private const string BuiltinPrefix = "builtin://";
    private const string ImportPrefix = "import://";
    private const string SchemaName = "rule pack";

    // The generated validator composes the nested validators, so they are built here instead of resolved from DI:
    // ZeroAlloc.Validation.Inject would add a package and a constructor parameter for stateless objects.
    private static readonly RulePackValidator Validator = new(
        new LayerDefinitionValidator(),
        new ComponentRoleDefinitionValidator(),
        new CodeFeatureRuleDefinitionValidator(),
        new RuleDefinitionValidator(new TargetSelectorValidator(), new TargetSelectorValidator(), new RuleExceptionValidator()),
        new LayerRetargetValidator(),
        new RuleAmendmentValidator(new RuleExceptionValidator()),
        new NamingConventionsValidator());

    private PackOperations()
    {
    }

    /// <summary>Gets the instrumented operations the public pack services call.</summary>
    public static IPackOperations Instrumented { get; } = new PackOperationsInstrumented(new PackOperations());

    public Result<RulePack, AriadneError> LoadFromString(string yaml) => Checked(
        YamlDocument.Parse<RulePack>(yaml, "Rule pack YAML", SchemaName), "Rule pack YAML");

    public Result<RulePack, AriadneError> LoadFromFile(string filePath) => Checked(
        YamlDocument.ReadFile<RulePack>(filePath, "Rule pack", SchemaName), YamlDocument.FileSubject(filePath, "Rule pack"));

    public Result<RulePack, AriadneError> GetPack(string name, BuiltinPackCatalog catalog)
    {
        lock (catalog.Lock)
        {
            if (catalog.Cache.TryGetValue(name, out var cached))
            {
                return Result<RulePack, AriadneError>.Success(cached);
            }

            if (!BuiltinPackCatalog.TryGetResource(name, out var resourceName))
            {
                return Result<RulePack, AriadneError>.Failure(new AriadneError(
                    AriadneErrorCode.NotFound,
                    $"Built-in pack '{name}' not found.",
                    $"Available packs: {string.Join(", ", BuiltinPackCatalog.Names)}"));
            }

            using var stream = typeof(PackOperations).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' could not be loaded.");

            using var reader = new StreamReader(stream);
            var pack = catalog.Loader.LoadFromString(reader.ReadToEnd());
            if (pack.IsSuccess)
            {
                catalog.Cache[name] = pack.Value;
            }

            return pack;
        }
    }

    public Result<RulePack, AriadneError> Resolve(
        RulePack userPack,
        string? workingDirectory,
        IBuiltinPackRegistry registry,
        IRulePackLoader loader,
        IRulePackMerger merger)
    {
        if (userPack.Extends.Count == 0)
        {
            return RulePackReferenceCheck.Check(userPack);
        }

        var packs = new List<RulePack>();

        // Stop at the first reference that fails: the user fixes it and runs again.
        foreach (var reference in userPack.Extends)
        {
            var pack = LoadReference(reference, workingDirectory, registry, loader);
            if (pack.IsFailure)
            {
                return pack;
            }

            packs.Add(pack.Value);
        }

        packs.Add(userPack);

        var merged = merger.Merge(packs);
        return merged.IsFailure ? merged : RulePackReferenceCheck.Check(merged.Value);
    }

    private static Result<RulePack, AriadneError> LoadReference(
        string reference, string? workingDirectory, IBuiltinPackRegistry registry, IRulePackLoader loader)
    {
        if (reference.StartsWith(BuiltinPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return registry.GetPack(reference[BuiltinPrefix.Length..]);
        }

        if (reference.StartsWith(ImportPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return LoadImportedPack(reference[ImportPrefix.Length..], workingDirectory, loader);
        }

        return Result<RulePack, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.Invalid,
            $"Unsupported extends reference: '{reference}'. Only 'builtin://' and 'import://' references are currently supported."));
    }

    private static Result<RulePack, AriadneError> LoadImportedPack(string importName, string? workingDirectory, IRulePackLoader loader)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Invalid,
                $"Cannot resolve import '{importName}' without a working directory. Run 'ariadne sync' first to fetch imported packs."));
        }

        // Sync refuses the same names, so a name that leaves the cache can only be a hand-written reference.
        var cacheRoot = Path.Combine(workingDirectory, ".architecture", ".cache", "git");
        if (!ImportNameRule.IsSingleDirectoryName(importName, cacheRoot))
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Invalid,
                ImportNameRule.BadNameMessage(importName)));
        }

        var cacheDir = Path.Combine(cacheRoot, importName);

        if (!Directory.Exists(cacheDir))
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                $"Cache directory for import '{importName}' not found. Run 'ariadne sync' to fetch imported packs.",
                cacheDir));
        }

        string[] yamlFiles;
        try
        {
            yamlFiles = Directory.GetFiles(cacheDir, "*.yaml", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A directory without read permission, or one removed since the check above, lands here.
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Unavailable,
                $"Cache directory for import '{importName}' could not be read at '{cacheDir}'.",
                ex.Message));
        }

        if (yamlFiles.Length == 0)
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                $"No YAML files found in the cache directory for import '{importName}'. Run 'ariadne sync' to fetch imported packs.",
                cacheDir));
        }

        return loader.LoadFromFile(yamlFiles[0]);
    }

    /// <summary>Runs the member rules on a parsed pack; <paramref name="subject"/> names a nameless pack.</summary>
    private static Result<RulePack, AriadneError> Checked(Result<RulePack, AriadneError> parsed, string subject) =>
        parsed.IsFailure ? parsed : CheckMembers(parsed.Value, subject);

    /// <summary>Runs the member rules declared on the schema types; cross-references are the resolver's job.</summary>
    private static Result<RulePack, AriadneError> CheckMembers(RulePack pack, string subject)
    {
        var result = Validator.Validate(pack);
        if (result.IsValid)
        {
            return Result<RulePack, AriadneError>.Success(pack);
        }

        var lines = new List<string>(result.Failures.Length);
        foreach (ref readonly var failure in result.Failures)
        {
            // EPS06 is wrong here: ValidationFailure is a readonly struct, so reading its members copies nothing.
            // ZeroAlloc.Validation suppresses the same finding in its own generated code, citing its issue 213.
#pragma warning disable EPS06
            lines.Add($"{failure.PropertyName}: {failure.ErrorMessage}");
#pragma warning restore EPS06
        }

        return Result<RulePack, AriadneError>.Failure(RulePackError.Invalid(pack.Name, lines, subject));
    }

}
