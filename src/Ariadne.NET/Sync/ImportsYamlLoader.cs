using Ariadne.NET.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

[Singleton]
public sealed class ImportsYamlLoader : IImportsLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public Result<ImportsManifest, AriadneError> LoadFromString(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        return Parse(yaml, "Imports YAML");
    }

    public Result<ImportsManifest, AriadneError> LoadFromFile(string filePath)
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
            return Failure(new AriadneError(
                AriadneErrorCode.Unavailable,
                $"Imports file '{filePath}' could not be read.",
                ex.Message));
        }

        return Parse(yaml, $"Imports file '{filePath}'");
    }

    /// <summary>Deserialises <paramref name="yaml"/>; <paramref name="subject"/> starts every error message.</summary>
    private Result<ImportsManifest, AriadneError> Parse(string yaml, string subject)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return Empty(subject);
        }

        ImportsManifest? manifest;
        try
        {
            manifest = _deserializer.Deserialize<ImportsManifest?>(yaml);
        }
        catch (YamlException ex)
        {
            return Failure(YamlFailure.ToError(ex, subject, "imports"));
        }

        if (manifest is null)
        {
            return Empty(subject);
        }

        // 'imports: ~' and '- ~' deserialise to nulls that the sync code would dereference.
        if (manifest.Imports is null || manifest.Imports.Any(import => import is null))
        {
            return Failure(new AriadneError(
                AriadneErrorCode.Invalid,
                $"{subject} has an empty entry under 'imports'; give each import a name, type, url and version, or write 'imports: []'."));
        }

        return Result<ImportsManifest, AriadneError>.Success(manifest);
    }

    private static Result<ImportsManifest, AriadneError> Failure(AriadneError error) =>
        Result<ImportsManifest, AriadneError>.Failure(error);

    private static Result<ImportsManifest, AriadneError> Empty(string subject) =>
        Failure(new AriadneError(AriadneErrorCode.Invalid, $"{subject} is empty."));

    private static Result<ImportsManifest, AriadneError> NotFound(string filePath) =>
        Failure(new AriadneError(
            AriadneErrorCode.NotFound,
            $"Imports file '{filePath}' not found.",
            filePath));
}
