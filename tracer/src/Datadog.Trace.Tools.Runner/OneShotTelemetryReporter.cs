// <copyright file="OneShotTelemetryReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Tools.Runner.Checks;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    /// <summary>
    /// Sends information to Datadog's instrumentation-telemetry Logs endpoint.
    /// The behavior of this class is different from the standard <see cref="TelemetryController"/> in that:
    ///  1. It attempts to send the telemetry synchronously in one shot, circling through the different transports until one succeeds.
    ///  2. It is only meant to be used from within dd-trace tool, and reads its configuration for the process that is being diagnosed by it.
    /// </summary>
    internal class OneShotTelemetryReporter
    {
        private readonly TelemetryTransportManager _telemetryTransportManager;

        internal OneShotTelemetryReporter(ITelemetryTransport[] transports)
        {
            _telemetryTransportManager = new TelemetryTransportManager(transports, maxFatalErrors: 1, maxTransientErrors: 1);
        }

        internal static OneShotTelemetryReporter CreateForProcess(ProcessInfo process)
        {
            var settings = new ImmutableExporterSettings(new ExporterSettings(process.Configuration));
            var transports = TelemetryTransportFactory.Create(TelemetrySettings.FromSource(process.Configuration, () => true), settings);
            return new OneShotTelemetryReporter(transports);
        }

        public async Task<bool> UploadAsLogMessage(string logContents, ApplicationTelemetryData applicationData)
        {
            var telemetryData = BuildLogTelemetryData(logContents, applicationData);
            return await UploadTelemetryData(telemetryData).ConfigureAwait(false);
        }

        private async Task<bool> UploadTelemetryData(TelemetryData telemetryData)
        {
            while (!_telemetryTransportManager.HasSentSuccessfully)
            {
                bool keepTrying = await _telemetryTransportManager.TryPushTelemetry(telemetryData).ConfigureAwait(false);
                if (!keepTrying)
                {
                    return false;
                }
            }

            return true;
        }

        private TelemetryData BuildLogTelemetryData(string logContents, ApplicationTelemetryData applicationData)
        {
            var telemetryDataBuilder = new TelemetryDataBuilder();
            var hostData = ConfigurationTelemetryCollector.CreateHostTelemetryData();
            var logsPayload = new LogsPayload() { new() { Message = logContents, Level = TelemetryLogLevel.ERROR } };
            return telemetryDataBuilder.BuildLogsTelemetryData(applicationData, hostData, logsPayload);
        }
    }
}
