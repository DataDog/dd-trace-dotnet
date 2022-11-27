// <copyright file="AnalyzeProcessMemoryCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Tools.Runner.Checks;
using Datadog.Trace.Tools.Runner.DumpAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using static Datadog.Trace.Tools.Runner.Checks.Resources;

namespace Datadog.Trace.Tools.Runner
{
    internal class AnalyzeProcessMemoryCommand : AsyncCommand<AnalyzeProcessMemorySettings>
    {
        public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeProcessMemorySettings settings)
        {
            AnsiConsole.Record();
            AnsiConsole.WriteLine("Running checks on process " + settings.Pid);

            var process = ProcessInfo.GetProcessInfo(settings.Pid);
            try
            {
                if (process == null)
                {
                    Utils.WriteError("Could not fetch information about target process. Make sure to run the command from an elevated prompt, and check that the pid is correct.");
                    return 1;
                }

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

            AnsiConsole.MarkupLine(success ?
                                       "[green]Successfully uploaded telemetry data to Datadog.[/]" :
                                       "[red]Failed to upload telemetry data to Datadog.[/]");
        }

        private static ApplicationTelemetryData GetApplicationData(ProcessInfo process)
        {
            // Note that the Service Name and Environment are taken from Universal Service Tagging (the DD_SERVICE and DD_ENV environment variables).
            // We currently don't try to obtain the inferred service name in case DD_SERVICE is missing.
            var tracerSettings = new ImmutableTracerSettings(process.Configuration);
            return new ApplicationTelemetryData(tracerSettings.ServiceName, tracerSettings.Environment, TracerConstants.AssemblyVersion, "dotnet", "?");
        }
    }
}
