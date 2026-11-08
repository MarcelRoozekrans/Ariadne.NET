namespace Ariadne.NET.Hooks.Tests;

/// <summary>A unique session id whose recorded files are cleared on dispose, since the test project runs in parallel.</summary>
internal sealed class TestSession : IDisposable
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    public void Dispose() => EditedFiles.Clear(Id);
}
