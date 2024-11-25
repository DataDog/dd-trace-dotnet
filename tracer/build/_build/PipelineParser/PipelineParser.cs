using System.IO;
using Nuke.Common.IO;
using YamlDotNet.Serialization.NamingConventions;
using Logger = Serilog.Log;
static class PipelineParser
{
    public static PipelineDefinition GetPipelineDefinition(AbsolutePath rootDirectory)
    {
        var pipelineYaml = rootDirectory / ".azure-pipelines" / "ultimate-pipeline.yml";
        Logger.Information("Reading {PipelineYaml} YAML file", pipelineYaml);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                          .WithNamingConvention(CamelCaseNamingConvention.Instance)
                          .IgnoreUnmatchedProperties()
                          .Build();

        using var sr = new StreamReader(pipelineYaml);
        return deserializer.Deserialize<PipelineDefinition>(sr);
    }
}
