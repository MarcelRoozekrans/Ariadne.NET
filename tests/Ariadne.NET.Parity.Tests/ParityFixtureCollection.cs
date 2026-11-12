namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Test classes that build or read the ArchUnitNET parity fixtures under <c>tests/fixtures/archunit-parity</c>. The fixture
/// build writes each project's <c>bin</c> and <c>obj</c> folders, so the classes run one after the other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ParityFixtureCollection
{
    public const string Name = "Parity fixture";
}
