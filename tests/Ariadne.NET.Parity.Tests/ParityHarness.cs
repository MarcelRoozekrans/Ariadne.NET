using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Testing;
using Ariadne.NET.Tests.Shared;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Runs a parity case through ArchUnitNET and Ariadne and compares what each flags. Both sides yield canonical type names, or
/// full slice namespaces for <see cref="Projection.Slices"/>. The ArchUnitNET <see cref="Architecture"/> and Ariadne's
/// <see cref="ArchitectureReport"/> are built once per kind.
/// </summary>
internal static class ParityHarness
{
    private const string CompilerGeneratedAttribute = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

    private static readonly ConcurrentDictionary<string, Lazy<Architecture>> Architectures = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Lazy<ArchitectureReport>> Reports = new(StringComparer.Ordinal);

    // Canonical names of every loaded type that carries CompilerGeneratedAttribute, so SourceTypesOnly can drop them by name.
    private static readonly ConcurrentDictionary<string, byte> CompilerGenerated = new(StringComparer.Ordinal);

    // Canonical type name to namespace, per kind, for projecting Ariadne's source types onto slices.
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Namespaces = new(StringComparer.Ordinal);

    private static readonly Lazy<ServiceProvider> Services = new(
        () => new ServiceCollection().AddAriadneNETServices().BuildServiceProvider(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// What Ariadne checks for <paramref name="kind"/>: the kind's <c>Parity.&lt;Kind&gt;.slnx</c> when it has one, for a kind
    /// that needs a second assembly, else its one project.
    /// </summary>
    public static string KindProject(string kind)
    {
        var solution = Path.Combine(FixtureProjects.ParityRoot, kind, $"Parity.{kind}.slnx");
        return File.Exists(solution) ? solution : Path.Combine(FixtureProjects.ParityRoot, kind, $"Parity.{kind}.csproj");
    }

    /// <summary>The pack of <paramref name="kind"/>.</summary>
    public static string KindPack(string kind) =>
        Path.Combine(FixtureProjects.ParityRoot, kind, ".architecture", "rules.yaml");

    /// <summary>True when <paramref name="caseId"/> has a <see cref="KnownGaps"/> entry.</summary>
    public static bool IsKnownGap(string caseId) =>
        KnownGaps.Entries.Any(g => string.Equals(g.CaseId, caseId, StringComparison.Ordinal));

    /// <summary>True when <paramref name="name"/> is <paramref name="ns"/> itself or lies below it.</summary>
    public static bool IsIn(string name, string ns) =>
        string.Equals(name, ns, StringComparison.Ordinal) || name.StartsWith(ns + ".", StringComparison.Ordinal);

    /// <summary>What ArchUnitNET flags for <paramref name="c"/>.</summary>
    public static IReadOnlySet<string> ArchUnitNetFailures(ParityCase c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var architecture = ArchitectureOf(c.Kind);
        var failed = c.Rule(architecture).Evaluate(architecture).Where(r => !r.Passed).Select(r => r.EvaluatedObject);

        if (c.Projection == Projection.Slices)
        {
            var prefix = SlicePrefix(c);
            return failed.SelectMany(SlicesOf).Select(s => string.IsNullOrEmpty(s.NameSpace) ? prefix + s.Identifier.Identifier : s.Identifier.Identifier)
                .ToHashSet(StringComparer.Ordinal);
        }

        var names = failed.Select(o => o is IType type
            ? type.FullName
            : throw new InvalidOperationException($"Case '{c.Id}': ArchUnitNET reported a {o?.GetType().Name ?? "null"}, not a type."));
        return SourceTypesOnly(names).Select(TypeNames.Canonical).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>What Ariadne flags for <paramref name="c"/>, from the kind's cached report.</summary>
    /// <exception cref="ArchitectureCheckException">The check could not run, for example because the pack does not load.</exception>
    public static IReadOnlySet<string> AriadneFailures(ParityCase c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var report = Reports.GetOrAdd(
            c.Kind, kind => new Lazy<ArchitectureReport>(() => ArchitectureCheck.Run(KindProject(kind)), LazyThreadSafetyMode.ExecutionAndPublication));
        return FromReport(c, report.Value);
    }

    /// <summary>What Ariadne flags for <paramref name="c"/> on another copy of its project, uncached.</summary>
    public static IReadOnlySet<string> AriadneFailures(ParityCase c, string project)
    {
        ArgumentNullException.ThrowIfNull(c);
        return FromReport(c, ArchitectureCheck.Run(project));
    }

    /// <summary>
    /// Projects <paramref name="report"/> for <paramref name="c"/>. Throws when a project was skipped or a violation belongs to
    /// a rule outside the kind, so neither can read as "no violations".
    /// </summary>
    public static IReadOnlySet<string> FromReport(ParityCase c, ArchitectureReport report)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(report);
        if (report.SkippedProjects.Count > 0)
        {
            throw new InvalidOperationException(
                $"Case '{c.Id}': Ariadne skipped {string.Join("; ", report.SkippedProjects.Select(p => $"{p.Name}: {p.Reason}"))}.");
        }

        var kindRule = "PARITY-" + c.Kind.ToUpperInvariant();
        var foreign = report.Violations.Where(v => !IsKindRule(v.RuleId, kindRule)).Select(v => $"{v.RuleId}: {v.Message}").ToList();
        if (foreign.Count > 0)
        {
            throw new InvalidOperationException(
                $"Case '{c.Id}': Ariadne reported violations of rules outside {kindRule}: {string.Join("; ", foreign)}");
        }

        var sources = report.Violations
            .Where(v => string.Equals(v.RuleId, c.AriadneRuleId, StringComparison.Ordinal))
            .Select(v => TypeNames.Canonical(v.SourceType));
        return c.Projection == Projection.Slices
            ? sources.Select(t => SliceOf(c, t)).ToHashSet(StringComparer.Ordinal)
            : sources.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Drops compiler-generated types: names with a <c>&lt;</c>, and types of any fixture kind that carry
    /// <c>CompilerGeneratedAttribute</c>. Loads every kind's architecture first, so the result does not depend on which
    /// kinds were loaded before.
    /// </summary>
    public static IEnumerable<string> SourceTypesOnly(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        foreach (var kind in ParityCases.All.Select(c => c.Kind).Distinct(StringComparer.Ordinal))
        {
            ArchitectureOf(kind);
        }

        return names.Where(n => !n.Contains('<', StringComparison.Ordinal) && !CompilerGenerated.ContainsKey(TypeNames.Canonical(n)));
    }

    /// <summary>True when the pack of <paramref name="kind"/> defines a rule or layer with id <paramref name="ruleId"/>.</summary>
    public static bool PackHasRule(string kind, string ruleId)
    {
        var loaded = Services.Value.GetRequiredService<IRulePackLoader>().LoadFromFile(KindPack(kind));
        if (loaded.IsFailure)
        {
            throw new InvalidOperationException($"The pack of '{kind}' does not load: {loaded.Error.Message} {loaded.Error.Detail}");
        }

        var pack = loaded.Value;
        return pack.Rules.Any(r => string.Equals(r.Id, ruleId, StringComparison.Ordinal))
            || pack.Layers.Any(l => string.Equals(l.Id, ruleId, StringComparison.Ordinal));
    }

    /// <summary>Asserts that ArchUnitNET and Ariadne agree on <paramref name="c"/>, or differ exactly as its divergence records.</summary>
    public static void AssertAgrees(ParityCase c)
    {
        ArgumentNullException.ThrowIfNull(c);
        AssertAgrees(c, ArchUnitNetFailures(c), AriadneFailures(c));
    }

    /// <summary>
    /// Asserts the two answers agree, or differ exactly as the case's <see cref="Divergences"/> entry records. Either way,
    /// nothing in the case's clean namespace is flagged and both tools flag something in its broken namespace.
    /// </summary>
    public static void AssertAgrees(ParityCase c, IReadOnlySet<string> archUnitNet, IReadOnlySet<string> ariadne)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(archUnitNet);
        ArgumentNullException.ThrowIfNull(ariadne);

        var divergence = Divergences.Entries.FirstOrDefault(d => string.Equals(d.CaseId, c.Id, StringComparison.Ordinal));
        if (divergence is null)
        {
            ariadne.Should().BeEquivalentTo(archUnitNet, "Ariadne must flag what ArchUnitNET flags in case '{0}'", c.Id);
        }
        else
        {
            AssertDivergence(c, divergence, archUnitNet, ariadne);
        }

        AssertCleanAndBroken(c, "ArchUnitNET", archUnitNet);
        AssertCleanAndBroken(c, "Ariadne", ariadne);
    }

    // No name in the clean namespace, and at least one in the broken namespace.
    private static void AssertCleanAndBroken(ParityCase c, string tool, IReadOnlySet<string> names)
    {
        names.Where(n => IsIn(n, c.CleanNamespace)).Should().BeEmpty(
            "{0} must not flag the {1} namespace in case '{2}'", tool, c.CleanNamespace, c.Id);
        names.Should().Contain(
            n => IsIn(n, c.BrokenNamespace), "{0} must flag the {1} namespace in case '{2}'", tool, c.BrokenNamespace, c.Id);
    }

    /// <summary>Asserts the two answers differ by exactly the names <paramref name="d"/> records.</summary>
    public static void AssertDivergence(ParityCase c, Divergence d, IReadOnlySet<string> archUnitNet, IReadOnlySet<string> ariadne)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(archUnitNet);
        ArgumentNullException.ThrowIfNull(ariadne);

        archUnitNet.Except(ariadne, StringComparer.Ordinal).Should().BeEquivalentTo(
            d.ArchUnitNetOnly, "only ArchUnitNET must flag the names the divergence of case '{0}' records", c.Id);
        ariadne.Except(archUnitNet, StringComparer.Ordinal).Should().BeEquivalentTo(
            d.AriadneOnly, "only Ariadne must flag the names the divergence of case '{0}' records", c.Id);
    }

    /// <summary>Asserts the kind's pack has no rule for gap case <paramref name="c"/>, so the gap entry is not stale.</summary>
    public static void AssertGapHasNoRule(ParityCase c)
    {
        ArgumentNullException.ThrowIfNull(c);
        PackHasRule(c.Kind, c.AriadneRuleId).Should().BeFalse(
            "the pack of '{0}' now has {1}, so the KnownGaps entry for '{2}' must be deleted", c.Kind, c.AriadneRuleId, c.Id);
    }

    private static bool IsKindRule(string ruleId, string kindRule) =>
        string.Equals(ruleId, kindRule, StringComparison.Ordinal) || ruleId.StartsWith(kindRule + "-", StringComparison.Ordinal);

    private static Architecture ArchitectureOf(string kind) =>
        Architectures.GetOrAdd(kind, k => new Lazy<Architecture>(() => Load(k), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static Architecture Load(string kind)
    {
        // A path-based load reads the DLL with Mono.Cecil instead of loading it into the test process, which would lock it.
        var output = Path.Combine(FixtureProjects.ParityRoot, kind, "bin", "Debug", "net10.0");
        var dll = $"Parity.{kind}.dll";
        if (!File.Exists(Path.Combine(output, dll)))
        {
            throw new FileNotFoundException($"The parity fixture '{kind}' has no build output.", Path.Combine(output, dll));
        }

        // Parity.<Kind>*.dll also takes the kind's second assembly, such as Parity.PResideInAssembly.Other.dll, which the
        // build copies next to the kind's own. No kind references another, so no other kind's DLL sits in this folder.
        var architecture = new ArchLoader().LoadFilteredDirectory(output, $"Parity.{kind}*.dll", SearchOption.TopDirectoryOnly).Build();
        var namespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in architecture.Types)
        {
            var canonical = TypeNames.Canonical(type.FullName);
            if (IsCompilerGenerated(type))
            {
                CompilerGenerated.TryAdd(canonical, 0);
            }

            namespaces[canonical] = type.Namespace.FullName;
        }

        Namespaces[kind] = namespaces;
        return architecture;
    }

    private static bool IsCompilerGenerated(IType type) =>
        type.IsCompilerGenerated
        || type.Attributes.Any(a => string.Equals(a.FullName, CompilerGeneratedAttribute, StringComparison.Ordinal));

    // The slices a failed result names: the slice itself, or every slice of a reported cycle.
    private static IEnumerable<Slice> SlicesOf(object? evaluated) => evaluated switch
    {
        Slice slice => [slice],
        IEnumerable many => many.Cast<object?>().SelectMany(SlicesOf),
        _ => throw new InvalidOperationException($"ArchUnitNET reported a {evaluated?.GetType().Name ?? "null"}, not a slice."),
    };

    // The literal part of the slice pattern before its capture, such as "Parity.SNoCycles.Broken." for "Parity.SNoCycles.Broken.(*)".
    private static string SlicePrefix(ParityCase c)
    {
        var pattern = c.SlicePattern ?? throw new InvalidOperationException($"Case '{c.Id}' projects slices but has no SlicePattern.");
        var capture = pattern.IndexOf('(', StringComparison.Ordinal);
        return capture < 0
            ? throw new InvalidOperationException($"Case '{c.Id}': slice pattern '{pattern}' has no capture.")
            : pattern[..capture];
    }

    // The full namespace of the slice a type falls in, read as ArchUnitNET reads it: the whole namespace remainder after the
    // pattern's literal prefix, so the slice is the type's own namespace.
    private static string SliceOf(ParityCase c, string canonicalType)
    {
        ArchitectureOf(c.Kind);
        if (!Namespaces[c.Kind].TryGetValue(canonicalType, out var ns))
        {
            throw new InvalidOperationException($"Case '{c.Id}': Ariadne flagged '{canonicalType}', which the {c.Kind} assembly does not declare.");
        }

        var prefix = SlicePrefix(c);
        if (!ns.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"Case '{c.Id}': Ariadne flagged '{canonicalType}' in namespace '{ns}', which no slice of '{c.SlicePattern}' holds."));
        }

        return ns;
    }
}
