// <copyright file="TelemetryReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    /// <summary>
    /// Sends information to Datadog's instrumentation-telemetry Logs endpoint.
    /// Note: This assumes that the environment variables that control how we communicate with the Datadog Agent are set.
    /// </summary>
    internal static class TelemetryReporter
    {
        public static async Task UploadAsLogMessage(string results)
        {
            var telemetryData = BuildLogTelemetryData(results);
            if (await UploadTelemetryData(telemetryData).ConfigureAwait(false))
            {
                AnsiConsole.MarkupLine("[green]Successfully uploaded telemetry data to Datadog.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to upload telemetry data to Datadog.[/]");
            }
        }

        private static async Task<bool> UploadTelemetryData(TelemetryData telemetryData)
        {
            var settings = TelemetrySettings.FromDefaultSources();
            var transports = TelemetryTransportFactory.Create(settings, new ImmutableExporterSettings(new EnvironmentConfigurationSource()));
            var telemetryTransportManager = new TelemetryTransportManager(transports, maxFatalErrors: 1, maxTransientErrors: 1);

            while (!telemetryTransportManager.HasSentSuccessfully)
            {
                bool keepTrying = await telemetryTransportManager.TryPushTelemetry(telemetryData).ConfigureAwait(false);
                if (!keepTrying)
                {
                    return false;
                }
            }

            return true;
        }

        private static TelemetryData BuildLogTelemetryData(string results)
        {
            var telemetryDataBuilder = new TelemetryDataBuilder();

            var applicationData = new ApplicationTelemetryData("service", "env", "tracerVersion", "dotnet", "languageVersion");
            var hostData = ConfigurationTelemetryCollector.CreateHostTelemetryData();
            var logsPayload = new LogsPayload() { new() { Message = results, Level = TelemetryLogLevel.DEBUG } };
            return telemetryDataBuilder.BuildLogsTelemetryData(applicationData, hostData, logsPayload);
        }
    }
}
