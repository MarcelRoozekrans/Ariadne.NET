using Ariadne.NET.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using ZInject;

namespace Ariadne.NET.RuleLoader;

[Singleton]
public sealed class YamlRulePackLoader : IRulePackLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public RulePack LoadFromString(string yaml)
    {
        return _deserializer.Deserialize<RulePack>(yaml);
    }

    public RulePack LoadFromFile(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        return LoadFromString(yaml);
    }
}
