using Ariadne.NET.Abstractions;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using ZeroAlloc.Results;

namespace Ariadne.NET;

/// <summary>
/// Reads and deserialises one YAML document the way the reflection-based loaders do: the file-read failures, an empty
/// document and a YamlDotNet failure all become the same errors. Rule packs use only <see cref="ReadText"/>; they are
/// parsed by the strict pack reader instead.
/// </summary>
internal static class YamlDocument
{
    // A built deserializer holds no per-call state, so every loader shares this one.
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>The subject that starts the messages about the file at <paramref name="filePath"/>.</summary>
    /// <param name="filePath">The file the document is read from.</param>
    /// <param name="fileKind">What the file holds, such as <c>Rule pack</c> or <c>Imports</c>.</param>
    internal static string FileSubject(string filePath, string fileKind) => $"{fileKind} file '{filePath}'";

    /// <summary>
    /// Reads the file at <paramref name="filePath"/> and parses it as a <typeparamref name="T"/>. A missing file is
    /// NotFound; one that cannot be read is Unavailable; the rest is <see cref="Parse{T}"/>.
    /// </summary>
    internal static Result<T, AriadneError> ReadFile<T>(string filePath, string fileKind, string schemaName)
        where T : class
    {
        var text = ReadText(filePath, fileKind);
        return text.IsFailure
            ? Result<T, AriadneError>.Failure(text.Error)
            : Parse<T>(text.Value, FileSubject(filePath, fileKind), schemaName);
    }

    /// <summary>
    /// Reads the text of the file at <paramref name="filePath"/>. A missing file is NotFound; one that cannot be read
    /// is Unavailable. Both messages start with the <see cref="FileSubject"/>.
    /// </summary>
    internal static Result<string, AriadneError> ReadText(string filePath, string fileKind)
    {
        var subject = FileSubject(filePath, fileKind);

        try
        {
            return Result<string, AriadneError>.Success(File.ReadAllText(filePath));
        }
        catch (FileNotFoundException)
        {
            return NotFound(subject, filePath);
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound(subject, filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // On Linux a path that names a directory, or a file without read permission, lands here.
            return Result<string, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Unavailable,
                $"{subject} could not be read.",
                ex.Message));
        }
    }

    /// <summary>
    /// Deserialises <paramref name="yaml"/>; <paramref name="subject"/> starts every error message and
    /// <paramref name="schemaName"/> names the expected shape. Blank text and a null document are both empty.
    /// </summary>
    internal static Result<T, AriadneError> Parse<T>(string yaml, string subject, string schemaName)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return Empty<T>(subject);
        }

        T? document;
        try
        {
            document = Deserializer.Deserialize<T?>(yaml);
        }
        catch (YamlException ex)
        {
            return Result<T, AriadneError>.Failure(YamlFailure.ToError(ex, subject, schemaName));
        }

        return document is null ? Empty<T>(subject) : Result<T, AriadneError>.Success(document);
    }

    private static Result<T, AriadneError> Empty<T>(string subject) =>
        Result<T, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, $"{subject} is empty."));

    private static Result<string, AriadneError> NotFound(string subject, string filePath) =>
        Result<string, AriadneError>.Failure(new AriadneError(AriadneErrorCode.NotFound, $"{subject} not found.", filePath));
}
