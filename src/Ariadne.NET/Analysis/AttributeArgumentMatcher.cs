using System.Globalization;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// One compiled <see cref="AttributeArgumentMatch"/>: matches a single attribute instance whose class matches the
/// optional glob, whose constructor or named arguments hold every listed value in any position, and whose named
/// arguments hold every listed name with its value.
/// </summary>
internal sealed class AttributeArgumentMatcher
{
    private readonly Glob? _attribute;
    private readonly string[] _arguments;
    private readonly KeyValuePair<string, string>[] _named;

    private AttributeArgumentMatcher(Glob? attribute, string[] arguments, KeyValuePair<string, string>[] named)
    {
        _attribute = attribute;
        _arguments = arguments;
        _named = named;
    }

    /// <summary>Compiles the entry. The loader validates the attribute glob, so an invalid one is a programming error.</summary>
    /// <param name="entry">The entry to compile.</param>
    /// <returns>The matcher.</returns>
    /// <exception cref="ArgumentException">The attribute glob is invalid.</exception>
    public static AttributeArgumentMatcher From(AttributeArgumentMatch entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var attribute = entry.Attribute is { } pattern ? Glob.CompileAll([pattern], Glob.Dotted)[0] : null;
        return new AttributeArgumentMatcher(attribute, [.. entry.Arguments], [.. entry.Named]);
    }

    /// <summary>Whether the attribute instance satisfies the whole entry on its own.</summary>
    /// <param name="instance">One attribute applied directly to a type.</param>
    /// <param name="names">The shared name cache of the check.</param>
    /// <returns>True when the class, every value and every named value match this instance.</returns>
    public bool Matches(AttributeData instance, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(names);

        if (_attribute is not null
            && (instance.AttributeClass is not { } attributeClass || !_attribute.IsMatch(names.FullNameOf(attributeClass.OriginalDefinition))))
        {
            return false;
        }

        foreach (var (name, value) in _named)
        {
            if (!HasNamed(instance, name, value, names))
            {
                return false;
            }
        }

        foreach (var value in _arguments)
        {
            if (!HasValue(instance, value, names))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether the argument value equals the scalar text of a pack, by the rules of its kind.</summary>
    /// <param name="constant">The constructor or named argument value.</param>
    /// <param name="text">The scalar from the pack.</param>
    /// <param name="names">The shared name cache of the check.</param>
    /// <returns>True when they are equal; never for an array or an error constant.</returns>
    internal static bool ValueEquals(TypedConstant constant, string text, TypeNames names)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(names);

        return constant.Kind switch
        {
            TypedConstantKind.Primitive => PrimitiveEquals(constant.Value, text),
            TypedConstantKind.Enum => EnumEquals(constant, text, names),
            TypedConstantKind.Type => constant.Value is INamedTypeSymbol { TypeKind: not TypeKind.Error } type
                && string.Equals(names.FullNameOf(type.OriginalDefinition), text, StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool HasNamed(AttributeData instance, string name, string value, TypeNames names)
    {
        foreach (var argument in instance.NamedArguments)
        {
            if (string.Equals(argument.Key, name, StringComparison.Ordinal) && ValueEquals(argument.Value, value, names))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasValue(AttributeData instance, string value, TypeNames names)
    {
        foreach (var argument in instance.ConstructorArguments)
        {
            if (ValueEquals(argument, value, names))
            {
                return true;
            }
        }

        foreach (var argument in instance.NamedArguments)
        {
            if (ValueEquals(argument.Value, value, names))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PrimitiveEquals(object? value, string text) => value switch
    {
        string s => string.Equals(s, text, StringComparison.Ordinal),
        char c => text.Length == 1 && text[0] == c,
        bool b => string.Equals(text, b ? "true" : "false", StringComparison.OrdinalIgnoreCase),
        float f => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed.Equals(f),
        double d => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed.Equals(d),
        _ => IntegralEquals(value, text),
    };

    private static bool EnumEquals(TypedConstant constant, string text, TypeNames names)
    {
        if (IntegralEquals(constant.Value, text))
        {
            return true;
        }

        if (constant.Type is not INamedTypeSymbol enumType || constant.Value is null)
        {
            return false;
        }

        var fullName = names.FullNameOf(enumType.OriginalDefinition);
        foreach (var member in enumType.GetMembers())
        {
            if (member is not IFieldSymbol { HasConstantValue: true } field || !Equals(field.ConstantValue, constant.Value))
            {
                continue;
            }

            if (string.Equals(text, field.Name, StringComparison.Ordinal))
            {
                return true;
            }

            if (text.Length == fullName.Length + 1 + field.Name.Length
                && text.StartsWith(fullName, StringComparison.Ordinal)
                && text[fullName.Length] == '.'
                && text.EndsWith(field.Name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Exact and bounded: a fraction of only zeros is dropped, then the rest must parse as long or ulong. An exponent or
    // a fraction that is not zero never equals an integral constant. No double, decimal or BigInteger is involved.
    private static bool IntegralEquals(object? value, string text)
    {
        var length = LengthWithoutZeroFraction(text);
        if (length <= 0)
        {
            return false;
        }

        if (AsSigned(value) is { } signed)
        {
            return long.TryParse(text.AsSpan(0, length), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) && parsed == signed;
        }

        if (AsUnsigned(value) is { } unsigned)
        {
            return ulong.TryParse(text.AsSpan(0, length), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) && parsed == unsigned;
        }

        return false;
    }

    // The length of the text before a fraction made only of zeros, or -1 when a fraction holds another character.
    private static int LengthWithoutZeroFraction(string text)
    {
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
        {
            return text.Length;
        }

        for (var i = dot + 1; i < text.Length; i++)
        {
            if (text[i] != '0')
            {
                return -1;
            }
        }

        return dot;
    }

    private static long? AsSigned(object? value) => value switch
    {
        sbyte v => v,
        short v => v,
        int v => v,
        long v => v,
        _ => null,
    };

    private static ulong? AsUnsigned(object? value) => value switch
    {
        byte v => v,
        ushort v => v,
        uint v => v,
        ulong v => v,
        _ => null,
    };
}
