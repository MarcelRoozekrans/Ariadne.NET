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
        catch (YamlException ex) when (ex is SyntaxErrorException or SemanticErrorException)
        {
            // The parser rejected the text itself; its message is a readable reason with no CLR type names.
            return Invalid($"{subject} could not be parsed: {ex.Message}", ex);
        }
        catch (YamlException ex)
        {
            // YamlDotNet wraps exceptions raised while converting nodes to the schema types, so YAML of the wrong
            // shape arrives here too. Their text names CLR types, so the message is fixed and only the location shows.
            return Invalid($"{subject} does not match the rule pack schema.", ex);
        }

        return pack is null ? Empty(subject) : Result<RulePack, AriadneError>.Success(pack);
    }

    private static Result<RulePack, AriadneError> Invalid(string message, YamlException ex) =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.Invalid,
            message,
            $"line {ex.Start.Line}, column {ex.Start.Column}"));

    private static Result<RulePack, AriadneError> Empty(string subject) =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, $"{subject} is empty."));

    private static Result<RulePack, AriadneError> NotFound(string filePath) =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.NotFound,
            $"Rule pack file '{filePath}' not found.",
            filePath));
}
