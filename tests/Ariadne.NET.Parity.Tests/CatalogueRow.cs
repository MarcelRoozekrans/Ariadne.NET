namespace Ariadne.NET.Parity.Tests;

/// <summary>One row of the ArchUnitNET parity catalogue page.</summary>
/// <param name="Id">The row id, such as <c>p-reside-in-namespace</c>, without backticks.</param>
/// <param name="ArchUnitNet">The ArchUnitNET methods, as written on the page.</param>
/// <param name="Ariadne">The Ariadne pack construct, as written on the page, or <c>—</c>.</param>
/// <param name="Status">One of <c>covered</c>, <c>gap</c>, <c>divergence</c>, <c>deferred</c>, <c>out-of-scope</c> and <c>ariadne-only</c>, without backticks.</param>
/// <param name="Notes">The notes cell, as written on the page.</param>
public sealed record CatalogueRow(string Id, string ArchUnitNet, string Ariadne, string Status, string Notes);
