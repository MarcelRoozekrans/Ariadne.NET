using Ariadne.NET.Abstractions;
using YamlDotNet.Core;

namespace Ariadne.NET;

/// <summary>Maps a YamlDotNet failure to the error every YAML loader reports.</summary>
internal static class YamlFailure
{
    /// <summary>
    /// <paramref name="subject"/> starts the message; <paramref name="schemaName"/> names the expected shape.
    /// </summary>
    internal static AriadneError ToError(YamlException ex, string subject, string schemaName)
    {
        // The parser rejecting the text itself gives a readable reason with no CLR type names. Anything else is
        // YamlDotNet wrapping an exception raised while converting nodes to the schema types: its text names CLR
        // types, so the message is fixed and only the location shows.
        var message = ex is SyntaxErrorException or SemanticErrorException
            ? $"{subject} could not be parsed: {ex.Message}"
            : $"{subject} does not match the {schemaName} schema.";

        return new AriadneError(
            AriadneErrorCode.Invalid,
            message,
            $"line {ex.Start.Line}, column {ex.Start.Column}");
    }
}
