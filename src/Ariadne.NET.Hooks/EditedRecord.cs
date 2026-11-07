namespace Ariadne.NET.Hooks;

/// <summary>One recorded edit as <see cref="EditedFiles.ReadRecords"/> saw it: the record's file and the path it holds.</summary>
internal readonly record struct EditedRecord(string File, string Path);
