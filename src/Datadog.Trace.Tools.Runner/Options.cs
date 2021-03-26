using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace Datadog.Trace.Tools.Runner
{
    internal class Options
    {
        [Usage(ApplicationAlias = "dd-trace")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Set CI environment variables", UnParserSettings.WithUseEqualTokenOnly(), new Options { SetEnvironmentVariables = true });
                yield return new Example("Wrap a command with the CLR profiler environment variables", UnParserSettings.WithUseEqualTokenOnly(), new Options { Value = new string[] { "dotnet", "test" } });
                yield return new Example("Wrap a command with the CLR profiler and change the datadog environment key", UnParserSettings.WithUseEqualTokenOnly(), new Options { Environment = "ci", Value = new string[] { "dotnet", "test" } });
                yield return new Example("Wrap a command with the CLR profiler and changing the datadog agent url", UnParserSettings.WithUseEqualTokenOnly(), new Options { AgentUrl = "http://agent:8126", Value = new string[] { "dotnet", "test" } });
            }
        }

        [Option("set-ci", Required = false, Default = false, HelpText = "Setup the clr profiler environment variables for the CI job and exit. (only supported in Azure Pipelines)")]
        public bool SetEnvironmentVariables { get; set; }

        [Option("dd-env", Required = false, HelpText = "Sets the environment name for the unified service tagging.")]
        public string Environment { get; set; }

        [Option("dd-service", Required = false, HelpText = "Sets the service name for the unified service tagging.")]
        public string Service { get; set; }

        [Option("dd-version", Required = false, HelpText = "Sets the version name for the unified service tagging.")]
        public string Version { get; set; }

        [Option("agent-url", Required = false, HelpText = "Datadog trace agent url.")]
        public string AgentUrl { get; set; }

        [Option("tracer-home", Required = false, HelpText = "Sets the tracer home folder path.")]
        public string TracerHomeFolder { get; set; }

        [Option("crank-import", HelpText = "Import crank Json results file.")]
        public string CrankImportFile { get; set; }

        [Value(0, Required = false, Hidden = true, HelpText = "Command to be wrapped by the cli tool.")]
        public IEnumerable<string> Value { get; set; }
    }
}
