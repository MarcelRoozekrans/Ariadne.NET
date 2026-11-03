using Ariadne.NET.Abstractions;

using ZeroAlloc.Inject;

namespace Ariadne.NET.RuleEngine;

[Singleton]
public sealed class LayerInferenceEngine : ILayerInferenceEngine
{
    public LayerInferenceResult Infer(RulePack resolvedPack, string roleName)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(roleName);

        // Search by primary Name (case-insensitive)
        var nameMatches = resolvedPack.ComponentRoles
            .Where(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nameMatches.Count == 1)
        {
            return new LayerInferenceResult
            {
                Status = LayerInferenceStatus.Ok,
                Layer = nameMatches[0].Layer,
                RoleName = nameMatches[0].Name,
            };
        }

        if (nameMatches.Count > 1)
        {
            return new LayerInferenceResult
            {
                Status = LayerInferenceStatus.Ambiguous,
                AmbiguousOptions = nameMatches.Select(r => r.Name + " (" + r.Layer + ")").ToList(),
                Message = "Multiple roles match '" + roleName + "' by name.",
            };
        }

        // Search by AlternativeNames (case-insensitive)
        var altMatches = resolvedPack.ComponentRoles
            .Where(r => r.AlternativeNames.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (altMatches.Count == 1)
        {
            return new LayerInferenceResult
            {
                Status = LayerInferenceStatus.Ok,
                Layer = altMatches[0].Layer,
                RoleName = altMatches[0].Name,
            };
        }

        if (altMatches.Count > 1)
        {
            return new LayerInferenceResult
            {
                Status = LayerInferenceStatus.Ambiguous,
                AmbiguousOptions = altMatches.Select(r => r.Name + " (" + r.Layer + ")").ToList(),
                Message = "Multiple roles match '" + roleName + "' by alternative name.",
            };
        }

        return new LayerInferenceResult
        {
            Status = LayerInferenceStatus.Invalid,
            Message = "Unknown role: " + roleName,
        };
    }
}
