namespace Ariadne.NET.Abstractions;

public interface IBuiltinPackRegistry
{
    IReadOnlyList<string> GetAvailablePackNames();
    RulePack GetPack(string name);
}
