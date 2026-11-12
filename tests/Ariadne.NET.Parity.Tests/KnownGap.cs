namespace Ariadne.NET.Parity.Tests;

/// <summary>A case Ariadne cannot express yet.</summary>
/// <param name="CaseId">The parity case id.</param>
/// <param name="Phase">The phase that closes the gap, such as <c>4.2</c>.</param>
/// <param name="Reason">Why Ariadne has no rule for it yet.</param>
public sealed record KnownGap(string CaseId, string Phase, string Reason);
