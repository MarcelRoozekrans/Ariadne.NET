namespace Ariadne.NET.Hooks;

/// <summary>One edited path as <see cref="EditedFiles.ReadRecords"/> saw it, with every record file that holds it.</summary>
internal sealed record EditedRecord(string Path, IReadOnlyList<string> Files);
