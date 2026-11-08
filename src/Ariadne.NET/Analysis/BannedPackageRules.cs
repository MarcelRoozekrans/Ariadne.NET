using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Analysis;

/// <summary>The <c>bannedPackages</c> rules of a pack, evaluated over the resolved packages of each project.</summary>
internal static class BannedPackageRules
{
    private const int MaxPathHops = 6;
    private const string PathSeparator = " -> ";

    /// <summary>
    /// Returns one violation per rule, covered project and matching package, with the project as source and the package id as
    /// target and no location. Package ids match case-insensitively with the dotted glob rules, as NuGet ids are case-insensitive.
    /// A project is covered when the rule's <c>from.assemblies</c> is empty or matches, and not excepted. Assembly names are
    /// those of the compilation, or the project name when it has none. A project whose packages are not known yields nothing,
    /// because <see cref="RequirePackages"/> has rejected the run before.
    /// </summary>
    /// <param name="pack">The resolved pack.</param>
    /// <param name="project">The project to check.</param>
    /// <returns>The violations, in rule and package order.</returns>
    public static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, AnalyzedProject project)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(project);

        var violations = new List<ArchitectureViolation>();
        if (project.Packages is not { } packages)
        {
            return violations;
        }

        var assembly = AssemblyOf(project);
        foreach (var rule in pack.Rules)
        {
            if (rule.Kind != RuleKind.BannedPackages || !Covers(rule, assembly))
            {
                continue;
            }

            var banned = Compile(rule.Packages);
            foreach (var package in packages)
            {
                if (AnyMatch(banned, package.Id))
                {
                    violations.Add(Violation(rule, project, package));
                }
            }
        }

        return violations;
    }

    /// <summary>Rejects a run in which a bannedPackages rule covers a project whose packages are not known, since a clean report would claim what was not checked.</summary>
    /// <param name="pack">The resolved pack.</param>
    /// <param name="projects">The projects of the run.</param>
    /// <exception cref="ArgumentException">A covered project has no package list.</exception>
    public static void RequirePackages(RulePack pack, IReadOnlyList<AnalyzedProject> projects)
    {
        foreach (var rule in pack.Rules)
        {
            if (rule.Kind != RuleKind.BannedPackages)
            {
                continue;
            }

            foreach (var project in projects)
            {
                if (project is { Packages: null } && Covers(rule, AssemblyOf(project)))
                {
                    throw new ArgumentException(
                        $"Rule '{rule.Id}' bans packages, but the packages of project '{project.Name}' are not known; analyse the project with its restored package list.",
                        nameof(projects));
                }
            }
        }
    }

    private static string AssemblyOf(AnalyzedProject project) => project.Compilation.AssemblyName ?? project.Name;

    private static bool Covers(RuleDefinition rule, string assembly)
    {
        if (rule.From.Assemblies.Count > 0 && !AnyMatch(Compile(rule.From.Assemblies, Glob.Flat), assembly))
        {
            return false;
        }

        foreach (var exception in rule.Exceptions)
        {
            if (exception.Assemblies.Count > 0 && AnyMatch(Compile(exception.Assemblies, Glob.Flat), assembly))
            {
                return false;
            }
        }

        return true;
    }

    private static Glob[] Compile(IList<string> patterns) => Compile(patterns, pattern => Glob.Dotted(pattern, ignoreCase: true));

    private static Glob[] Compile(IList<string> patterns, Func<string, ZeroAlloc.Results.Result<Glob, string>> compile)
    {
        var globs = new Glob[patterns.Count];
        for (var i = 0; i < globs.Length; i++)
        {
            var result = compile(patterns[i]);
            if (result.IsFailure)
            {
                throw new ArgumentException(result.Error, nameof(patterns));
            }

            globs[i] = result.Value;
        }

        return globs;
    }

    private static bool AnyMatch(Glob[] globs, string name)
    {
        foreach (var glob in globs)
        {
            if (glob.IsMatch(name))
            {
                return true;
            }
        }

        return false;
    }

    private static ArchitectureViolation Violation(RuleDefinition rule, AnalyzedProject project, ResolvedPackage package) =>
        new()
        {
            RuleId = rule.Id,
            Kind = ViolationKind.BannedPackage,
            SourceType = project.Name,
            Target = package.Id,
            Because = rule.Because,
            Message = string.Create(
                CultureInfo.InvariantCulture,
                $"{project.Name} references {package.Id} {package.Version} {Route(package)}, which rule '{rule.Id}' bans"),
        };

    private static string Route(ResolvedPackage package)
    {
        if (package.IsDirect)
        {
            return "directly";
        }

        // The package itself closes the path, and the message names it already.
        var hops = new List<string>(package.Path);
        if (hops.Count > 0 && string.Equals(hops[^1], package.Id, StringComparison.OrdinalIgnoreCase))
        {
            hops.RemoveAt(hops.Count - 1);
        }

        return hops.Count == 0 ? "transitively" : "through " + Join(hops);
    }

    private static string Join(List<string> hops)
    {
        if (hops.Count <= MaxPathHops)
        {
            return string.Join(PathSeparator, hops);
        }

        var head = MaxPathHops / 2;
        var tail = MaxPathHops - head;
        var builder = new StringBuilder(string.Join(PathSeparator, hops.GetRange(0, head)));
        builder.Append(PathSeparator).Append(CultureInfo.InvariantCulture, $"... ({hops.Count - MaxPathHops} more hops left out)").Append(PathSeparator);
        builder.Append(string.Join(PathSeparator, hops.GetRange(hops.Count - tail, tail)));
        return builder.ToString();
    }
}
