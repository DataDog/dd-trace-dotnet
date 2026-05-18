// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Threading;

namespace Datadog.Trace.Telemetry
{
    internal sealed class TelemetryFactory
    {
        private static IMetricsTelemetryCollector _metrics = NullMetricsTelemetryCollector.Instance;

        private TelemetryFactory()
        {
        }

        public static TelemetryFactory Instance { get; } = new();

        public static IMetricsTelemetryCollector Metrics => Volatile.Read(ref _metrics);

        internal static IMetricsTelemetryCollector SetMetricsForTesting(IMetricsTelemetryCollector telemetry)
            => Interlocked.Exchange(ref _metrics, telemetry);

        public ITelemetryController CreateTelemetryController()
            => NullTelemetryController.Instance;
    }
}
