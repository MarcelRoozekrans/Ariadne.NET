using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

/// <summary>How syncing one import ended. Each import in a run succeeds or fails on its own.</summary>
public sealed record ImportSyncOutcome(string ImportName, Result<SyncedImport, AriadneError> Result);
