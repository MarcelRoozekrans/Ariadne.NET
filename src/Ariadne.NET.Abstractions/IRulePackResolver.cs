namespace Ariadne.NET.Abstractions;

public interface IRulePackResolver
{
    RulePack Resolve(RulePack userPack);

    RulePack Resolve(RulePack userPack, string? workingDirectory);
}
