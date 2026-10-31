using Ariadne.NET.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.RuleLoader;

[Singleton]
public sealed class YamlRulePackLoader : IRulePackLoader
{
    // The generated validator composes the nested validators, so they are built here instead of resolved from DI:
    // ZeroAlloc.Validation.Inject would add a package and a constructor parameter for four stateless objects.
    private static readonly RulePackValidator Validator = new(
        new LayerDefinitionValidator(),
        new ComponentRoleDefinitionValidator(),
        new CodeFeatureRuleDefinitionValidator(),
        new NamingConventionsValidator());

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public Result<RulePack, AriadneError> LoadFromString(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        return Parse(yaml, "Rule pack YAML");
    }

    public Result<RulePack, AriadneError> LoadFromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        string yaml;
        try
        {
            yaml = File.ReadAllText(filePath);
        }
        catch (FileNotFoundException)
        {
            return NotFound(filePath);
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound(filePath);
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

    /// <summary>Deserialises <paramref name="yaml"/>; <paramref name="subject"/> starts every error message.</summary>
    private Result<RulePack, AriadneError> Parse(string yaml, string subject)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return Empty(subject);
        }

        RulePack? pack;
        try
        {
            pack = _deserializer.Deserialize<RulePack?>(yaml);
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

    private static Result<RulePack, AriadneError> NotFound(string filePath) =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.NotFound,
            $"Rule pack file '{filePath}' not found.",
            filePath));
}
