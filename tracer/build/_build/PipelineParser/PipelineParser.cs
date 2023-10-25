using System.IO;
using Nuke.Common.IO;
using YamlDotNet.Serialization.NamingConventions;

static class PipelineParser
{
    public static PipelineDefinition GetPipelineDefinition(AbsolutePath rootDirectory)
    {
        var pipelineYaml = rootDirectory / ".azure-pipelines" / "ultimate-pipeline.yml";
        Serilog.Log.Information("Reading {PipelineYaml} YAML file", pipelineYaml);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                          .WithNamingConvention(CamelCaseNamingConvention.Instance)
                          .IgnoreUnmatchedProperties()
                          .Build();

        using var sr = new StreamReader(pipelineYaml);
        return deserializer.Deserialize<PipelineDefinition>(sr);
    }
}
