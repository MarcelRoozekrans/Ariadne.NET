namespace Ariadne.NET.Workspaces.Tests;

/// <summary>
/// Test classes that load a fixture solution through <c>MSBuildWorkspace</c>. The design-time build writes files under
/// the fixture's <c>obj</c> directory (for example <c>AssemblyInfo.cs</c>), so two classes loading the same fixture at
/// the same time collide with a sharing violation on Windows. Putting them in one collection runs them one after the
/// other. A separate collection from the working-directory one keeps the intent of each readable.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FixtureSolutionCollection
{
    public const string Name = "Fixture solutions";
}
