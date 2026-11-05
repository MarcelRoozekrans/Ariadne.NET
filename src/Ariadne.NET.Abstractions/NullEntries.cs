namespace Ariadne.NET.Abstractions;

/// <summary>Shared body of the instance methods that <c>[Must]</c> requires on the schema types.</summary>
internal static class NullEntries
{
    public static bool None<T>(IList<T>? items) => items is null || !items.Any(static i => i is null);
}
