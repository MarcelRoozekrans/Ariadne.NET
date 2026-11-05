namespace Ariadne.NET.RuleLoader;

/// <summary>One problem in a pack file, at a 1-based line and column.</summary>
/// <param name="Line">The 1-based line of the offending node.</param>
/// <param name="Column">The 1-based column of the offending node.</param>
/// <param name="Message">What is wrong, in pack terms.</param>
internal readonly record struct PackProblem(int Line, int Column, string Message);
