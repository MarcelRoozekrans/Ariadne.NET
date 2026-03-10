using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchGov.Core.Sync;

public sealed class ImportsYamlLoader : IImportsLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ImportsManifest LoadFromString(string yaml)
    {
        return _deserializer.Deserialize<ImportsManifest>(yaml);
    }

    public ImportsManifest LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Imports file not found.", filePath);
        }

        var yaml = File.ReadAllText(filePath);
        return LoadFromString(yaml);
    }
}
