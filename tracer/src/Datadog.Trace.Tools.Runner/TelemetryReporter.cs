// <copyright file="TelemetryReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;

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
            var telemetryData = BuildTelemetryData(results);
            await UploadTelemetryData(telemetryData).ConfigureAwait(false);
        }

        private static async Task UploadTelemetryData(TelemetryData telemetryData)
        {
            var settings = TelemetrySettings.FromDefaultSources();
            var transports = TelemetryTransportFactory.Create(settings, ImmutableTracerSettings.FromDefaultSources().Exporter).Reverse().ToArray();
            var telemetryTransportManager = new TelemetryTransportManager(transports);
            await telemetryTransportManager.TryPushTelemetry(telemetryData).ConfigureAwait(false);
        }

        private static TelemetryData BuildTelemetryData(string results)
        {
            var telemetryDataBuilder = new TelemetryDataBuilder();

            var applicationData = new ApplicationTelemetryData("service", "env", "tracerVersion", "dotnet", "languageVersion");
            var hostData = ConfigurationTelemetryCollector.CreateHostTelemetryData();
            var logsPayload = new LogsPayload() { new() { Message = results, Level = TelemetryLogLevel.DEBUG } };
            return telemetryDataBuilder.BuildLogsTelemetryData(applicationData, hostData, logsPayload);
        }
    }
}
