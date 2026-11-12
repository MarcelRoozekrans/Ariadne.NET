using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;

namespace Ariadne.NET.Parity.Tests;

/// <summary>One ArchUnitNET rule and the Ariadne pack rule that should flag the same types.</summary>
/// <param name="Id">The catalogue row id, plus <c>:not</c> for the negative polarity.</param>
/// <param name="Kind">The fixture folder under <c>tests/fixtures/archunit-parity</c>.</param>
/// <param name="Prefix">The namespace prefix of the fixture, such as <c>Parity.CNotDependOnAny</c>.</param>
/// <param name="AriadneRuleId">The id of the rule in the kind's pack that this case compares.</param>
/// <param name="Projection">What the case compares.</param>
/// <param name="Rule">Builds the ArchUnitNET rule.</param>
/// <param name="SlicePattern">The ArchUnitNET slice pattern, for <see cref="Projection.Slices"/>.</param>
public sealed record ParityCase(
    string Id,
    string Kind,
    string Prefix,
    string AriadneRuleId,
    Projection Projection,
    Func<Architecture, IArchRule> Rule,
    string? SlicePattern = null)
{
    /// <summary>True for the negative polarity, whose fixture namespaces are <c>NotClean</c> and <c>NotBroken</c>.</summary>
    public bool IsNegative => Id.EndsWith(":not", StringComparison.Ordinal);

    /// <summary>The namespace whose types must be flagged.</summary>
    public string BrokenNamespace => Prefix + (IsNegative ? ".NotBroken" : ".Broken");

    /// <summary>The namespace whose types must never be flagged.</summary>
    public string CleanNamespace => Prefix + (IsNegative ? ".NotClean" : ".Clean");

    /// <inheritdoc />
    public override string ToString() => Id;
}
