using System.Globalization;
using System.Text;

namespace Ariadne.NET.Parity.Tests;

/// <summary>Maps Roslyn display strings and metadata names to one comparable form.</summary>
internal static class TypeNames
{
    /// <summary>
    /// Returns namespace and type names joined by '.', each generic type written with
    /// a backtick and its arity and no type arguments.
    /// </summary>
    public static string Canonical(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var result = new StringBuilder(name.Length);
        var angleDepth = 0;
        var bracketDepth = 0;
        var parenDepth = 0;
        var arity = 0;
        foreach (var c in name)
        {
            if (bracketDepth > 0)
            {
                if (c == '[') { bracketDepth++; }
                else if (c == ']') { bracketDepth--; }
                continue;
            }

            switch (c)
            {
                case '[':
                    bracketDepth++;
                    break;
                case '<':
                    if (angleDepth == 0) { arity = 1; }
                    angleDepth++;
                    break;
                case '>':
                    angleDepth--;
                    if (angleDepth == 0)
                    {
                        result.Append('`').Append(arity.ToString(CultureInfo.InvariantCulture));
                    }
                    break;
                case '(' when angleDepth > 0:
                    parenDepth++;
                    break;
                case ')' when angleDepth > 0:
                    parenDepth--;
                    break;
                case ',' when angleDepth == 1 && parenDepth == 0:
                    arity++;
                    break;
                default:
                    if (angleDepth > 0) { break; }
                    result.Append(c == '+' ? '.' : c);
                    break;
            }
        }

        return result.ToString();
    }
}
