namespace Ariadne.NET.Abstractions;

public interface IRulePackLoader
{
    RulePack LoadFromString(string yaml);
    RulePack LoadFromFile(string filePath);
}
