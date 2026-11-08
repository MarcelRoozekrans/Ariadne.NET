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

    /// <summary>Compiles the globs of every bannedPackages rule of the pack once, for the whole run.</summary>
    /// <param name="pack">The resolved pack.</param>
    /// <returns>The compiled rules.</returns>
    public static Compiled Compile(RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var rules = new List<CompiledRule>();
        foreach (var rule in pack.Rules)
        {
            if (rule.Kind == RuleKind.BannedPackages)
            {
                rules.Add(CompiledRule.From(rule));
            }
        }

        return new Compiled(rules);
    }

    /// <summary>The compiled bannedPackages rules of a pack.</summary>
    internal sealed class Compiled
    {
        private readonly List<CompiledRule> _rules;

        internal Compiled(List<CompiledRule> rules) => _rules = rules;

        /// <summary>
        /// Returns one violation per rule, covered project and matching package, with the project as source and the package id as
        /// target and no location. Package ids match case-insensitively with the dotted glob rules, as NuGet ids are case-insensitive.
        /// A project is covered when the rule's <c>from.assemblies</c> is empty or matches, and not excepted. Assembly names are
        /// those of the compilation, or the project name when it has none. A project whose packages are not known yields nothing,
        /// because <see cref="RequirePackages"/> has rejected the run before.
        /// </summary>
        /// <param name="project">The project to check.</param>
        /// <returns>The violations, in rule and package order.</returns>
        public IReadOnlyList<ArchitectureViolation> Check(AnalyzedProject project)
        {
            ArgumentNullException.ThrowIfNull(project);

            var violations = new List<ArchitectureViolation>();
            if (project.Packages is not { } packages)
            {
                return violations;
            }

            var assembly = AssemblyOf(project);
            foreach (ref readonly var rule in CollectionsMarshal.AsSpan(_rules))
            {
                if (!rule.Covers(assembly))
                {
                    continue;
                }

                foreach (var package in packages)
                {
                    if (Glob.AnyMatch(rule.Packages, package.Id))
                    {
                        violations.Add(Violation(rule.Rule, project, package));
                    }
                }
            }

            return violations;
        }

        /// <summary>Rejects a run in which a bannedPackages rule covers a project whose packages are not known, since a clean report would claim what was not checked. An excepted project is never checked, so it cannot trip this.</summary>
        /// <param name="projects">The projects of the run.</param>
        /// <param name="paramName">The argument of the public call that held them, named by the exception.</param>
        /// <exception cref="ArgumentException">A covered project has no package list.</exception>
        public void RequirePackages(IReadOnlyList<AnalyzedProject> projects, string paramName)
        {
            if (FindUncoveredProject(projects) is { } uncovered)
            {
                throw new ArgumentException(
                    $"Rule '{uncovered.RuleId}' bans packages, but the packages of project '{uncovered.ProjectName}' are not known; analyse the project with its restored package list.",
                    paramName);
            }
        }

        /// <summary>
        /// The first project, in rule then project order, that a bannedPackages rule covers while its packages are not known; null
        /// when there is none. <see cref="RequirePackages"/> throws on it, and a caller that loaded the projects itself checks it
        /// first to report the problem in its own terms.
        /// </summary>
        /// <param name="projects">The projects of the run.</param>
        /// <returns>The rule and the project, or null.</returns>
        internal UncoveredProject? FindUncoveredProject(IReadOnlyList<AnalyzedProject> projects)
        {
            ArgumentNullException.ThrowIfNull(projects);

            foreach (ref readonly var rule in CollectionsMarshal.AsSpan(_rules))
            {
                foreach (var project in projects)
                {
                    if (project is { Packages: null } && rule.Covers(AssemblyOf(project)))
                    {
                        return new UncoveredProject(rule.Rule.Id, project.Name);
                    }
                }
            }

            return null;
        }
    }

    /// <summary>A project that a bannedPackages rule covers while its packages are not known.</summary>
    /// <param name="RuleId">The id of the rule that covers the project.</param>
    /// <param name="ProjectName">The name of the project.</param>
    internal readonly record struct UncoveredProject(string RuleId, string ProjectName);

    internal sealed record CompiledRule(RuleDefinition Rule, Glob[] Packages, Glob[] FromAssemblies, Glob[][] ExceptionAssemblies)
    {
        public static CompiledRule From(RuleDefinition rule)
        {
            var exceptions = new Glob[rule.Exceptions.Count][];
            for (var i = 0; i < exceptions.Length; i++)
            {
                exceptions[i] = Glob.CompileAll(rule.Exceptions[i].Assemblies, Glob.Flat);
            }

            return new CompiledRule(
                rule,
                Glob.CompileAll(rule.Packages, pattern => Glob.Dotted(pattern, ignoreCase: true)),
                Glob.CompileAll(rule.From.Assemblies, Glob.Flat),
                exceptions);
        }

        // An empty from covers every project; an exception with no assemblies matches nothing.
        public bool Covers(string assembly)
        {
            if (FromAssemblies.Length > 0 && !Glob.AnyMatch(FromAssemblies, assembly))
            {
                return false;
            }

            foreach (var exception in ExceptionAssemblies)
            {
                if (exception.Length > 0 && Glob.AnyMatch(exception, assembly))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static string AssemblyOf(AnalyzedProject project) => project.Compilation.AssemblyName ?? project.Name;

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
