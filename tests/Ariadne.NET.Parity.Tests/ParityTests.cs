using Ariadne.NET.Tests.Shared;

namespace Ariadne.NET.Parity.Tests;

[Collection(ParityFixtureCollection.Name)]
public sealed class ParityTests
{
    public ParityTests() => FixtureProjects.EnsureParityBuilt();

    // A gap case has no Ariadne rule to compare; KnownGapTests covers it instead.
    [Theory]
    [MemberData(nameof(ParityCases.Ids), MemberType = typeof(ParityCases))]
    public void Case_agrees(string caseId)
    {
        if (ParityHarness.IsKnownGap(caseId))
        {
            return;
        }

        ParityHarness.AssertAgrees(ParityCases.Get(caseId));
    }
}
