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
    private const string EmptyMessage = "rules file is empty";

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public Result<RulePack, AriadneError> LoadFromString(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        if (string.IsNullOrWhiteSpace(yaml))
        {
            return Empty();
        }

        RulePack? pack;
        try
        {
            pack = _deserializer.Deserialize<RulePack?>(yaml);
        }
        catch (YamlException ex)
        {
            // Covers malformed YAML and YAML of the wrong shape, such as a sequence where a mapping belongs.
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Invalid,
                $"Rule pack YAML is invalid: {Reason(ex)}",
                $"line {ex.Start.Line}, column {ex.Start.Column}"));
        }

        return pack is null ? Empty() : Result<RulePack, AriadneError>.Success(pack);
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
        catch (IOException ex)
        {
            return Unreadable(filePath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            // On Linux a path that names a directory also lands here.
            return Unreadable(filePath, ex);
        }

        return LoadFromString(yaml);
    }

    private static Result<RulePack, AriadneError> NotFound(string filePath) =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.NotFound,
            "Rule pack file not found.",
            filePath));

    private static Result<RulePack, AriadneError> Unreadable(string filePath, Exception ex) =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(
            AriadneErrorCode.Unavailable,
            $"Rule pack file '{filePath}' could not be read.",
            ex.Message));

    private static Result<RulePack, AriadneError> Empty() =>
        Result<RulePack, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, EmptyMessage));

    private static string Reason(YamlException ex)
    {
        // YamlDotNet wraps the parser or conversion failure; the innermost exception names the problem.
        Exception innermost = ex;
        while (innermost.InnerException is not null)
        {
            innermost = innermost.InnerException;
        }

        return innermost.Message;
    }
}
