using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>
/// Matches one attribute instance applied directly to a type by the values of its arguments.
/// <see cref="Attribute"/>, <see cref="Arguments"/> and <see cref="Named"/> all apply to the same instance.
/// Values are scalar text: strings compare ordinally, a char as one-character text, bool as <c>true</c> or <c>false</c>
/// ignoring case, numbers numerically, an enum by simple member name, by the name qualified with the enum's full name
/// or by its underlying number, and <c>typeof</c> by the full name of the type. An array argument never matches.
/// </summary>
[Validate]
public sealed class AttributeArgumentMatch
{
    /// <summary>Dotted glob over the full name of the attribute class. When <see langword="null"/>, any attribute instance may match.</summary>
    public string? Attribute { get; init; }

    /// <summary>Values that must each equal some argument value of the instance, constructor or named, in any position.</summary>
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")]
    public IList<string> Arguments { get; init; } = [];

    /// <summary>Named arguments of the instance and the value each must equal, keyed by the argument name.</summary>
    [NotNull, Must(nameof(NamedIsValid), Message = "An attribute argument match needs arguments or named, and named must not contain null values.")]
    public IDictionary<string, string> Named { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    internal bool HasNoNullEntries(IList<string>? items) => NullEntries.None(items);

    internal bool NamedIsValid(IDictionary<string, string>? named)
    {
        if (named is null)
        {
            return false;
        }

        if (Arguments is not { Count: > 0 } && named.Count == 0)
        {
            return false;
        }

        return NullEntries.None(named.Values.ToList());
    }
}
