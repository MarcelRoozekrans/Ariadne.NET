using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// The type graph of one project and target framework, as the cross-project checks see it: with the project's name and the
/// graphs whose compilations its compilation references, which name the producer variants its build compiles against.
/// </summary>
/// <param name="Graph">The type graph.</param>
/// <param name="TargetFramework">The project's target framework, or null for a project that names none.</param>
/// <param name="Project">The project's name, shared by its target framework variants.</param>
/// <param name="References">The indices of the graphs this graph's compilation references, in reference order.</param>
internal readonly record struct ScopedGraph(TypeGraph Graph, string? TargetFramework, string Project, IReadOnlyList<int> References)
{
    /// <summary>
    /// Pairs each project with its graph and finds its references: a <see cref="CompilationReference"/> whose compilation is,
    /// by reference, another project's compilation. MSBuildWorkspace gives a C# project reference that way, pointing at the
    /// exact variant the build uses. Any other reference, or one to a compilation outside the run, is ignored.
    /// </summary>
    /// <param name="projects">The projects of the run.</param>
    /// <param name="graphs">The type graph of each project, in the same order.</param>
    /// <returns>One scoped graph per project, in input order.</returns>
    public static List<ScopedGraph> Of(IReadOnlyList<AnalyzedProject> projects, IReadOnlyList<TypeGraph> graphs)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(graphs);

        var indexOf = new Dictionary<Compilation, int>(projects.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < projects.Count; i++)
        {
            indexOf.TryAdd(projects[i].Compilation, i);
        }

        var result = new List<ScopedGraph>(projects.Count);
        for (var i = 0; i < projects.Count; i++)
        {
            var project = projects[i];
            var references = new List<int>();
            foreach (var reference in project.Compilation.References)
            {
                if (reference is CompilationReference { Compilation: var referenced }
                    && indexOf.TryGetValue(referenced, out var index)
                    && index != i
                    && !references.Contains(index))
                {
                    references.Add(index);
                }
            }

            result.Add(new ScopedGraph(graphs[i], project.TargetFramework, project.Name, references));
        }

        return result;
    }
}
