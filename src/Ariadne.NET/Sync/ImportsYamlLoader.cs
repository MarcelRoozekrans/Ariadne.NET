using Ariadne.NET.Abstractions;

using ZeroAlloc.Inject;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

[Singleton]
public sealed class ImportsYamlLoader : IImportsLoader
{
    private const string SchemaName = "imports";

    public Result<ImportsManifest, AriadneError> LoadFromString(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        return Checked(YamlDocument.Parse<ImportsManifest>(yaml, "Imports YAML", SchemaName), "Imports YAML");
    }

    public Result<ImportsManifest, AriadneError> LoadFromFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        return Checked(
            YamlDocument.ReadFile<ImportsManifest>(filePath, "Imports", SchemaName),
            YamlDocument.FileSubject(filePath, "Imports"));
    }

    /// <summary>Checks a parsed manifest; <paramref name="subject"/> starts the error message.</summary>
    private static Result<ImportsManifest, AriadneError> Checked(Result<ImportsManifest, AriadneError> parsed, string subject)
    {
        if (parsed.IsFailure)
        {
            return parsed;
        }

        // 'imports: ~' and '- ~' deserialise to nulls that the sync code would dereference.
        var manifest = parsed.Value;
        if (manifest.Imports is null || manifest.Imports.Any(import => import is null))
        {
            return Result<ImportsManifest, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Invalid,
                $"{subject} has an empty entry under 'imports'; give each import a name, type, url and version, or write 'imports: []'."));
        }

        return parsed;
    }
}
