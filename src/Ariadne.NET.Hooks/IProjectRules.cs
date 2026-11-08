using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Hooks;

/// <summary>Loads the resolved rule pack of the project under a root folder.</summary>
public interface IProjectRules
{
    /// <summary>Reads <c>root/.architecture/rules.yaml</c> and resolves it. A missing file is <c>NotFound</c>.</summary>
    Result<RulePack, AriadneError> Load(string root);
}
