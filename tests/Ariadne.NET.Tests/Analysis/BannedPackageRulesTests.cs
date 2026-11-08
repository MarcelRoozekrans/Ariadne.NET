using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class BannedPackageRulesTests
{
    private static readonly RulePack Pack = PackWith(["FluentValidation"], new TargetSelector());

    private static RulePack PackWith(IList<string> packages, TargetSelector from, params RuleException[] exceptions) => new()
    {
        Name = "test",
        Rules =
        [
            new RuleDefinition
            {
                Id = "TEST-BANNED",
                Because = "Validation goes through ZeroAlloc.Validation.",
                Kind = RuleKind.BannedPackages,
                Packages = packages,
                From = from,
                Exceptions = exceptions,
            },
        ],
    };

    private static AnalyzedProject Project(string name, string assembly, params ResolvedPackage[] packages) =>
        new(name, null, TestCompilation.Create("namespace Shop { public class A { } }").WithAssemblyName(assembly)) { Packages = packages };

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, AnalyzedProject project) =>
        BannedPackageRules.Compile(pack).Check(project);

    private static ResolvedPackage Direct(string id, string version = "11.9.0") => new(id, version, true, [id]);

    private static ResolvedPackage Transitive(string id, params string[] hops) => new(id, "11.9.0", false, [.. hops, id]);

    [Fact]
    public void DirectPackage_ReportsDirect()
    {
        var violations = Check(Pack, Project("Shop.Domain", "Shop.Domain", Direct("FluentValidation")));

        var v = violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.BannedPackage);
        v.RuleId.Should().Be("TEST-BANNED");
        v.Because.Should().Be("Validation goes through ZeroAlloc.Validation.");
        v.SourceType.Should().Be("Shop.Domain");
        v.Target.Should().Be("FluentValidation");
        v.Location.Should().BeNull();
        v.Message.Should().Be("Shop.Domain references FluentValidation 11.9.0 directly, which rule 'TEST-BANNED' bans");
    }

    [Fact]
    public void TransitivePackage_ReportsPathThroughParent()
    {
        var violations = Check(Pack, Project("Daedalus.Domain", "Daedalus.Domain", Transitive("FluentValidation", "ZeroAlloc.Validation.Compat")));

        violations.Should().ContainSingle().Which.Message.Should()
            .Be("Daedalus.Domain references FluentValidation 11.9.0 through ZeroAlloc.Validation.Compat, which rule 'TEST-BANNED' bans");
    }

    [Fact]
    public void TransitivePackage_LongPath_IsCappedAndSaysSo()
    {
        var hops = Enumerable.Range(1, 10).Select(i => $"Hop{i}").ToArray();

        var violations = Check(Pack, Project("App", "App", Transitive("FluentValidation", hops)));

        violations.Should().ContainSingle().Which.Message.Should()
            .Contain("through Hop1 -> Hop2 -> Hop3 -> ... (4 more hops left out) -> Hop8 -> Hop9 -> Hop10,");
    }

    [Fact]
    public void IdGlob_BansFamily()
    {
        var pack = PackWith(["Newtonsoft.**"], new TargetSelector());

        var violations = Check(pack, Project("App", "App", Direct("Newtonsoft.Json"), Direct("Newtonsoft.Json.Bson"), Direct("Other.Json")));

        violations.Select(v => v.Target).Should().Equal("Newtonsoft.Json", "Newtonsoft.Json.Bson");
        Check(pack, Project("App", "App", Direct("Newtonsoft"))).Should().ContainSingle();
    }

    [Theory]
    [InlineData("FluentValidation", "fluentvalidation")]
    [InlineData("fluentvalidation", "FluentValidation")]
    [InlineData("Fluent*", "FLUENTVALIDATION")]
    public void IdCaseDiffers_StillMatches(string pattern, string id)
    {
        var pack = PackWith([pattern], new TargetSelector());

        Check(pack, Project("App", "App", Direct(id))).Should().ContainSingle();
    }

    [Fact]
    public void FromAssemblies_ScopesProjects()
    {
        var pack = PackWith(["FluentValidation"], new TargetSelector { Assemblies = ["Shop.Domain"] });

        Check(pack, Project("Shop.Domain", "Shop.Domain", Direct("FluentValidation"))).Should().ContainSingle();
        Check(pack, Project("Shop.Web", "Shop.Web", Direct("FluentValidation"))).Should().BeEmpty();
    }

    [Fact]
    public void FromAssemblies_FallsBackToProjectNameWithoutAssemblyName()
    {
        var compilation = CSharpCompilation.Create(null, [CSharpSyntaxTree.ParseText("class A { }")]);
        var project = new AnalyzedProject("Shop.Domain", null, compilation) { Packages = [Direct("FluentValidation")] };
        var pack = PackWith(["FluentValidation"], new TargetSelector { Assemblies = ["Shop.Domain"] });

        Check(pack, project).Should().ContainSingle().Which.SourceType.Should().Be("Shop.Domain");
    }

    [Fact]
    public void ExceptionByAssembly_Suppresses()
    {
        var pack = PackWith(["FluentValidation"], new TargetSelector(), new RuleException { Because = "Legacy keeps it.", Assemblies = ["Shop.Legacy"] });

        Check(pack, Project("Shop.Legacy", "Shop.Legacy", Direct("FluentValidation"))).Should().BeEmpty();
        Check(pack, Project("Shop.Web", "Shop.Web", Direct("FluentValidation"))).Should().ContainSingle();
    }

    [Fact]
    public void PackageNotPresent_NoViolation()
    {
        Check(Pack, Project("App", "App", Direct("Serilog"))).Should().BeEmpty();
        Check(Pack, Project("App", "App")).Should().BeEmpty();
    }

    [Fact]
    public void PackagesNullInCoveredProject_Throws()
    {
        var validator = new ArchitectureValidator();
        var compilation = TestCompilation.Create("namespace Shop { public class A { } }");

        var single = () => validator.Validate(Pack, compilation);
        var many = () => validator.Validate(Pack, [new AnalyzedProject("Shop.Domain", null, compilation)]);

        single.Should().Throw<ArgumentException>().WithMessage("*TestAssembly*");
        many.Should().Throw<ArgumentException>().WithMessage("*Shop.Domain*");
    }

    [Fact]
    public void PackagesNullInUncoveredProject_DoesNotThrow()
    {
        var pack = PackWith(["FluentValidation"], new TargetSelector { Assemblies = ["Shop.Domain"] });
        var compilation = TestCompilation.Create("namespace Shop { public class A { } }");

        var report = new ArchitectureValidator().Validate(pack, [new AnalyzedProject("Shop.Web", null, compilation.WithAssemblyName("Shop.Web"))]);

        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PackagesNullWithoutBannedPackagesRule_DoesNotThrow()
    {
        var pack = new RulePack { Name = "test" };

        new ArchitectureValidator().Validate(pack, TestCompilation.Create("namespace Shop { public class A { } }")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SameViolationInTwoTargetFrameworks_ReportedOnce()
    {
        var compilation = TestCompilation.Create("namespace Shop { public class A { } }");
        var packages = new[] { Direct("FluentValidation") };

        var report = new ArchitectureValidator().Validate(Pack,
        [
            new AnalyzedProject("App", "net8.0", compilation) { Packages = packages },
            new AnalyzedProject("App", "net10.0", compilation) { Packages = packages },
        ]);

        var v = report.Violations.Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.BannedPackage);
        v.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void FromAssembliesGlob_StarCrossesDots()
    {
        var pack = PackWith(["FluentValidation"], new TargetSelector { Assemblies = ["Shop.*"] });

        Check(pack, Project("Shop.Domain.Core", "Shop.Domain.Core", Direct("FluentValidation"))).Should().ContainSingle();
        Check(pack, Project("Other", "Other", Direct("FluentValidation"))).Should().BeEmpty();
        Check(PackWith(["FluentValidation"], new TargetSelector { Assemblies = ["**"] }), Project("Other", "Other", Direct("FluentValidation"))).Should().ContainSingle();
    }

    [Fact]
    public void ExceptionWithoutAssemblies_MatchesNothing()
    {
        var pack = PackWith(["FluentValidation"], new TargetSelector(), new RuleException { Because = "Empty." });

        Check(pack, Project("App", "App", Direct("FluentValidation"))).Should().ContainSingle();
    }

    [Fact]
    public void PackagesNullInExceptedProject_DoesNotThrow()
    {
        var pack = PackWith(["FluentValidation"], new TargetSelector(), new RuleException { Because = "Legacy keeps it.", Assemblies = ["Shop.Legacy"] });
        var compilation = TestCompilation.Create("namespace Shop { public class A { } }").WithAssemblyName("Shop.Legacy");

        new ArchitectureValidator().Validate(pack, [new AnalyzedProject("Shop.Legacy", null, compilation)]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void TwoTargetFrameworks_DifferentVersions_ReportedOnce()
    {
        var compilation = TestCompilation.Create("namespace Shop { public class A { } }");

        var report = new ArchitectureValidator().Validate(Pack,
        [
            new AnalyzedProject("App", "net8.0", compilation) { Packages = [Direct("FluentValidation", "11.9.0")] },
            new AnalyzedProject("App", "net10.0", compilation) { Packages = [Direct("FluentValidation", "11.10.0")] },
        ]);

        var v = report.Violations.Should().ContainSingle().Subject;
        v.Message.Should().Contain("11.9.0");
        v.TargetFrameworks.Should().BeEmpty();
    }

    [Fact]
    public void ViolationInOneTargetFramework_NamesThatFramework()
    {
        var compilation = TestCompilation.Create("namespace Shop { public class A { } }");

        var report = new ArchitectureValidator().Validate(Pack,
        [
            new AnalyzedProject("App", "net8.0", compilation) { Packages = [Direct("FluentValidation")] },
            new AnalyzedProject("App", "net10.0", compilation) { Packages = [Direct("Serilog")] },
        ]);

        report.Violations.Should().ContainSingle().Which.TargetFrameworks.Should().Equal("net8.0");
    }
}
