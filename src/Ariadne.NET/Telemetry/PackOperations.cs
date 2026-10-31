using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using ZeroAlloc.Results;

namespace Ariadne.NET.Telemetry;

/// <summary>
/// The one implementation of the pack operations. <see cref="Instrumented"/> wraps it in the generated proxy; the public
/// <see cref="YamlRulePackLoader"/>, <see cref="BuiltinPackRegistry"/> and <see cref="RulePackResolver"/> call that proxy
/// and pass their own collaborators, so a resolve opens child spans for the packs it gets and loads through them.
/// </summary>
internal sealed class PackOperations : IPackOperations
{
    private const string BuiltinPrefix = "builtin://";
    private const string ImportPrefix = "import://";

    // The generated validator composes the nested validators, so they are built here instead of resolved from DI:
    // ZeroAlloc.Validation.Inject would add a package and a constructor parameter for four stateless objects.
    private static readonly RulePackValidator Validator = new(
        new LayerDefinitionValidator(),
        new ComponentRoleDefinitionValidator(),
        new CodeFeatureRuleDefinitionValidator(),
        new NamingConventionsValidator());

    // A built deserializer holds no per-call state, so every loader shares this one, as the container's singleton did.
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private PackOperations()
    {
    }

    /// <summary>Gets the instrumented operations the public pack services call.</summary>
    public static IPackOperations Instrumented { get; } = new PackOperationsInstrumented(new PackOperations());

    public Result<RulePack, AriadneError> LoadFromString(string yaml) => Parse(yaml, "Rule pack YAML");

    public Result<RulePack, AriadneError> LoadFromFile(string filePath)
    {
        string yaml;
        try
        {
            yaml = File.ReadAllText(filePath);
        }
        catch (FileNotFoundException)
        {
            return FileNotFound(filePath);
        }
        catch (DirectoryNotFoundException)
        {
            return FileNotFound(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // On Linux a path that names a directory, or a file without read permission, lands here.
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Unavailable,
                $"Rule pack file '{filePath}' could not be read.",
                ex.Message));
        }

        return Parse(yaml, $"Rule pack file '{filePath}'");
    }

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

        return RulePackReferenceCheck.Check(merger.Merge(packs));
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

        var cacheDir = Path.Combine(workingDirectory, ".architecture", ".cache", "git", importName);

        if (!Directory.Exists(cacheDir))
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                $"Cache directory for import '{importName}' not found. Run 'ariadne sync' to fetch imported packs.",
                cacheDir));
        }

        var yamlFiles = Directory.GetFiles(cacheDir, "*.yaml", SearchOption.TopDirectoryOnly);

        if (yamlFiles.Length == 0)
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                $"No YAML files found in the cache directory for import '{importName}'. Run 'ariadne sync' to fetch imported packs.",
                cacheDir));
        }

        return loader.LoadFromFile(yamlFiles[0]);
    }

    /// <summary>Deserialises <paramref name="yaml"/>; <paramref name="subject"/> starts every error message.</summary>
    private static Result<RulePack, AriadneError> Parse(string yaml, string subject)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return Empty(subject);
        }

        RulePack? pack;
        try
        {
            pack = Deserializer.Deserialize<RulePack?>(yaml);
        }
        catch (YamlException ex)
        {
            return Result<RulePack, AriadneError>.Failure(YamlFailure.ToError(ex, subject, "rule pack"));
        }

        return pack is null ? Empty(subject) : CheckMembers(pack, subject);
    }

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

        return Result<RulePack, AriadneError>.Failure(RulePackReferenceCheck.Invalid(pack.Name, lines, subject));
    }

    private static Result<RulePack, AriadneError> Empty(string subject) =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, $"{subject} is empty."));

    private static Result<RulePack, AriadneError> FileNotFound(string filePath) =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.NotFound,
            $"Rule pack file '{filePath}' not found.",
            filePath));
}
