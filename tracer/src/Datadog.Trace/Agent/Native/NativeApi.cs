// <copyright file="NativeApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Agent.Native
{
    internal class NativeApi : IApi
    {
        private static readonly IDatadogLogger StaticLog = DatadogLogging.GetLoggerFor<Api>();

        private readonly IDatadogLogger _log;
        private readonly bool _ready = false;

        public NativeApi(
            Action<Dictionary<string, float>> updateSampleRates,
            bool partialFlushEnabled,
            IDatadogLogger log = null)
        {
            // optionally injecting a log instance in here for testing purposes
            _log = log ?? StaticLog;
            _log.Debug("Creating new Api");

            try
            {
                if (!ExporterBindings.TryInitializeExporter(
                        "localhost",
                        8126,
                        TracerConstants.AssemblyVersion,
                        ".NET",
                        FrameworkDescription.Instance.ProductVersion,
                        FrameworkDescription.Instance.Name))
                {
                    _log.Error("Cannot configure the exporter");
                    return;
                }
            }
            catch (Exception e)
            {
                _log.Error(e, "An error happened while configuring transport");
                return;
            }

            _ready = true;

            // TODO: make sure we have this in libdd
            // new(HttpHeaderNames.TracingEnabled, "false"), // don't add automatic instrumentation to requests directed to the agent
            // new(ComputedTopLevelSpan, "1")

            ExporterBindings.SetSamplingRateCallback(updateSampleRates);
        }

        public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
        {
            if (!_ready)
            {
                return Task.FromResult(false);
            }

            _log.Debug("Sending stats to the Datadog Agent.");
            return Task.FromResult(false);
        }

        public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans)
        {
            if (!_ready)
            {
                return Task.FromResult(false);
            }

            _log.Debug<int>("Sending {Count} traces to the Datadog Agent.", numberOfTraces);

            ExporterBindings.SendTrace(traces.Array, numberOfTraces);
            return Task.FromResult(true);
        }
    }
}
