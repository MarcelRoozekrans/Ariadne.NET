using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// Parses template expressions such as <c>clean-architecture+cqrs</c>, where <c>+</c> composes built-in packs.
/// </summary>
public static class TemplateReference
{
    private const string BuiltinPrefix = "builtin://";

    public static Result<IList<string>, AriadneError> ParseNames(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var names = template.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (names.Length == 0)
        {
            return Result<IList<string>, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.Invalid,
                $"Template expression '{template}' contains no template names."));
        }

        return Result<IList<string>, AriadneError>.Success(names);
    }

    public static Result<IList<string>, AriadneError> ToExtends(string template)
    {
        var names = ParseNames(template);
        if (names.IsFailure)
        {
            return names;
        }

        return Result<IList<string>, AriadneError>.Success(
            names.Value.Select(name => BuiltinPrefix + name).ToList());
    }
}
