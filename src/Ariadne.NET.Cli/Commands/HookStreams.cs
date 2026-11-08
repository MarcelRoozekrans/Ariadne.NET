namespace Ariadne.NET.Cli.Commands;

/// <summary>The three byte streams a hook talks over; the real ones are the process's standard streams.</summary>
internal sealed record HookStreams(Stream Input, Stream Output, Stream Error);
