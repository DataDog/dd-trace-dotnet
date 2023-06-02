 // <copyright file="AnalyzeProcessMemoryCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Tools.Runner.Checks;
using Datadog.Trace.Tools.Runner.DumpAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class AnalyzeProcessMemoryCommand : AsyncCommand<AnalyzeProcessMemorySettings>
    {
        public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeProcessMemorySettings settings)
        {
            AnsiConsole.Record();
            AnsiConsole.WriteLine("Running checks on process " + settings.Pid);

            var process = ProcessInfo.GetProcessInfo(settings.Pid);
            if (process == null)
            {
                Utils.WriteError("Could not fetch information about target process. Make sure to run the command from an elevated prompt, and check that the pid is correct.");
                return 1;
            }

            try
            {
                ProcessMemoryAnalyzer.Analyze(process.Id);
            }
            finally
            {
                if (settings.UploadToDatadog)
                {
                    await UploadToInstrumentationTelemetry(process, AnsiConsole.ExportText()).ConfigureAwait(false);
                }
            }

            return 0;
        }

        private static async Task UploadToInstrumentationTelemetry(ProcessInfo process, string crashOrHangReport)
        {
            var telemetryReporter = OneShotTelemetryReporter.CreateForProcess(process);
            bool success = await telemetryReporter.UploadAsLogMessage(crashOrHangReport, GetApplicationData(process)).ConfigureAwait(false);

            AnsiConsole.MarkupLine(success ? "[green]Successfully uploaded telemetry data to Datadog.[/]" : "[red]Failed to upload telemetry data to Datadog.[/]");
        }

        private static ApplicationTelemetryData GetApplicationData(ProcessInfo process)
        {
            try
            {
                // Take the Service Name and Environment from Universal Service Tagging (the DD_SERVICE and DD_ENV environment variables) where available.
                var tracerSettings = new ImmutableTracerSettings(process.Configuration);
                return new ApplicationTelemetryData(tracerSettings.ServiceName ?? GetInferredServiceName(process), tracerSettings.Environment, TracerConstants.AssemblyVersion, "dotnet", InferLanguageVersion(process));
            }
            catch
            {
                // If something goes wrong in finding out the application metadata, we should still report the crash
                return new ApplicationTelemetryData(process.Name, "Unknown", "Unknown", "Unknown", "Unknown");
            }
        }

        private static string InferLanguageVersion(ProcessInfo process)
        {
            foreach (var processModule in process.Modules)
            {
                var fileName = Path.GetFileName(processModule);
                if (fileName is "mscorlib.dll" or "System.Private.CoreLib.dll")
                {
                    return GetAssemblyVersion(processModule);
                }
            }

            return "unknown";
        }

        private static string GetAssemblyVersion(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var reader = new PEReader(stream);
            var metadataReader = reader.GetMetadataReader();
            return metadataReader.GetAssemblyDefinition().Version.ToString();
        }

        private static string GetInferredServiceName(ProcessInfo process)
        {
            // Our inference here isn't completely accurate because we can't quite replicate the logic in <see cref="TraceManagerFactory.GetApplicationName"/>.
            return process.MainModule != null ? Path.GetFileName(process.MainModule) : process.Name;
        }
    }
}
