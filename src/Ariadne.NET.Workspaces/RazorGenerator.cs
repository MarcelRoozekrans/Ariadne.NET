namespace Ariadne.NET.Workspaces;

/// <summary>Recognises the documents of the Razor source generator, whose output Ariadne checks as hand-written code.</summary>
internal static class RazorGenerator
{
    /// <summary>The full type name of the Razor source generator, the same on every SDK; its assembly name changed between SDKs.</summary>
    public const string TypeName = "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator";

    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// True when the generated document came from the Razor generator. The workspace places a generated document at
    /// <c>&lt;generator assembly&gt;/&lt;generator type&gt;/&lt;hint name&gt;</c>, and Roslyn has no public API that names the
    /// generator, so the folder directly above the hint name is compared with <see cref="TypeName"/>.
    /// </summary>
    public static bool IsRazorDocument(string filePath, string hintName)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(hintName);

        var pathSegments = filePath.Split(Separators);
        var hintSegments = hintName.Split(Separators);
        var generatorIndex = pathSegments.Length - hintSegments.Length - 1;
        if (generatorIndex < 0)
        {
            return false;
        }

        for (var i = 0; i < hintSegments.Length; i++)
        {
            if (!string.Equals(pathSegments[generatorIndex + 1 + i], hintSegments[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return string.Equals(pathSegments[generatorIndex], TypeName, StringComparison.Ordinal);
    }
}
