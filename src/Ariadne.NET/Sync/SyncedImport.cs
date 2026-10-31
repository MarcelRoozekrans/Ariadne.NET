namespace Ariadne.NET.Sync;

/// <summary>An import that is in the cache at <paramref name="CachePath"/>, at <paramref name="ResolvedVersion"/>.</summary>
public sealed record SyncedImport(string ImportName, string ResolvedVersion, bool WasCached, string CachePath);
