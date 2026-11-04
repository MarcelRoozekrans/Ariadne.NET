namespace Ariadne.NET.Analysis;

/// <summary>A position in a source file.</summary>
/// <param name="FilePath">The path of the source file.</param>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Column">The 1-based column number.</param>
public readonly record struct SourceLocation(string FilePath, int Line, int Column);
