using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

/// <summary>
/// Checks the fields sync relies on. YAML does not enforce <c>required</c>, so a missing name, url or
/// version arrives as null.
/// </summary>
internal static class ImportDefinitionCheck
{
    public static UnitResult<AriadneError> Check(ImportDefinition import)
    {
        if (string.IsNullOrWhiteSpace(import.Name))
        {
            return Invalid("An import has no name; give every import in imports.yaml a name.");
        }

        // The name becomes a directory under the cache, which sync deletes and recreates.
        if (import.Name is "." or ".."
            || import.Name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return Invalid($"Import name '{import.Name}' must not contain path separators or be '.' or '..'.");
        }

        if (string.IsNullOrWhiteSpace(import.Url))
        {
            return Invalid($"Import '{import.Name}' has no url.");
        }

        // git would read a url starting with '-' as an option.
        if (import.Url.StartsWith('-'))
        {
            return Invalid($"Import '{import.Name}' has url '{import.Url}', which must not start with '-'.");
        }

        if (string.IsNullOrWhiteSpace(import.Version))
        {
            return Invalid($"Import '{import.Name}' has no version.");
        }

        return UnitResult<AriadneError>.Success();
    }

    private static UnitResult<AriadneError> Invalid(string message) =>
        UnitResult<AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, message));
}
