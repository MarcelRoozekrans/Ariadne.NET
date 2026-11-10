using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces.Tests;

[Collection(FixtureSolutionCollection.Name)]
public sealed class SolutionValidatorTests
{
    private readonly SolutionValidator _validator = new(new SolutionLoader(), new ArchitectureValidator());

    public SolutionValidatorTests() => FixtureProjects.EnsureRestored();

    [Fact]
    public async Task Validator_Clean_IsValid()
    {
        var report = await ValidateWithLayersAsync(["Domain", "WebApi"], "clean", "Shop.slnx");

        report.IsValid.Should().BeTrue();
        report.Projects.Select(p => p.Name).Should().BeEquivalentTo("Shop.Domain", "Shop.WebApi");
        report.TypesChecked.Should().Be(2);
        report.SkippedProjects.Should().BeEmpty();
    }

    [Fact]
    public async Task Validator_Broken_OneViolationWithCount3AndRelativeFile()
    {
        var report = await ValidateWithLayersAsync(["Domain", "Infrastructure"], "broken", "Shop.slnx");

        var violation = report.Violations.Should().ContainSingle().Which;
        violation.Kind.Should().Be(ViolationKind.DependencyNotAllowed);
        violation.SourceType.Should().Be("Shop.Domain.Order");
        violation.Target.Should().Be("Shop.Infrastructure.SqlStore");
        violation.ReferenceCount.Should().Be(3, "the field type, the object creation and the call to Save each reference SqlStore");
        violation.Location.Should().NotBeNull();
        violation.Location!.Value.FilePath.Should().EndWith(Path.Combine("Shop.Domain", "Order.cs"));
    }

    [Fact]
    public async Task Validator_Broken_NamesThePackOnEveryViolation()
    {
        var pack = TestPacks.Named("shop-rules", "Domain", "Infrastructure");

        var result = await _validator.ValidateAsync(pack, FixtureProjects.Path("broken", "Shop.slnx"));

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        result.Value.Violations.Should().NotBeEmpty().And.OnlyContain(v => v.Pack == "shop-rules");
    }

    [Fact]
    public async Task Validator_BrokenSlnfWithoutDomain_IsValid()
    {
        var report = await ValidateWithLayersAsync(["Infrastructure"], "broken", "Shop.Infrastructure-only.slnf");

        report.IsValid.Should().BeTrue();
        report.Projects.Select(p => p.Name).Should().Equal("Shop.Infrastructure");
    }

    [Fact]
    public async Task Validator_MultiTarget_ViolationListsNet10Only()
    {
        var report = await ValidateWithLayersAsync(["Domain", "Infrastructure"], "multitarget", "Multi.csproj");

        var violation = report.Violations.Should().ContainSingle().Which;
        violation.Target.Should().Be("Multi.Infrastructure.SqlStore");
        violation.TargetFrameworks.Should().Equal("net10.0");
        report.Projects.Should().ContainSingle().Which.TargetFrameworks.Should().Equal("net10.0", "net9.0");
    }

    [Fact]
    public async Task Validator_Generator_IsValid()
    {
        // The generated Registrations.cs sits in Domain and references Infrastructure; generated code is not a dependency source.
        var report = await ValidateWithLayersAsync(["Domain", "Infrastructure"], "generator", "Gen.App", "Gen.App.csproj");

        report.IsValid.Should().BeTrue();
        report.TypesChecked.Should().Be(2);
    }

    [Fact]
    public async Task Validator_DisposesWorkspace()
    {
        var path = FixtureProjects.Path("clean", "Shop.slnx");
        (await _validator.ValidateAsync(TestPacks.Layered(), path)).IsSuccess.Should().BeTrue();

        var second = await new SolutionLoader().LoadAsync(path);

        second.IsSuccess.Should().BeTrue(second.IsFailure ? second.Error.ToString() : null);
        second.Value.Dispose();
    }

    [Fact]
    public async Task Validator_DisposesLoadedSolutionAndCopiesSkippedProjects()
    {
        var owner = new DisposeSpy();
        var skipped = new SkippedProject("VbLib", "not a C# project");
        var loader = new StubLoader(Result<LoadedSolution, AriadneError>.Success(new LoadedSolution("/root", [], [skipped], owner)));
        var validator = new SolutionValidator(loader, new ArchitectureValidator());

        var result = await validator.ValidateAsync(TestPacks.Layered(), "any");

        result.IsSuccess.Should().BeTrue();
        result.Value.SkippedProjects.Should().Equal(skipped);
        owner.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Validator_LoadError_IsReturned()
    {
        var result = await _validator.ValidateAsync(TestPacks.Layered(), FixtureProjects.Path("unrestored", "Unrestored.csproj"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("run 'dotnet restore' first");
    }

    [Fact]
    public void AddAriadneNETWorkspaces_WithServices_ResolvesBothServices()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETWorkspaces();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISolutionLoader>().Should().BeOfType<SolutionLoader>();
        provider.GetRequiredService<ISolutionValidator>().Should().BeOfType<SolutionValidator>();
        provider.GetRequiredService<ISolutionValidator>().Should().BeSameAs(provider.GetRequiredService<ISolutionValidator>());
    }

    private async Task<ArchitectureReport> ValidateAsync(params string[] parts) =>
        await ValidateWithLayersAsync([], parts).ConfigureAwait(false);

    private async Task<ArchitectureReport> ValidateWithLayersAsync(string[] layers, params string[] parts)
    {
        var result = await _validator.ValidateAsync(TestPacks.Layered(layers), FixtureProjects.Path(parts)).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }

    private sealed class DisposeSpy : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class StubLoader(Result<LoadedSolution, AriadneError> result) : ISolutionLoader
    {
        public Task<Result<LoadedSolution, AriadneError>> LoadAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
