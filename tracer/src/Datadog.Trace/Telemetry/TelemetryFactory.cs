// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Threading;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Telemetry
{
    internal sealed class TelemetryFactory
    {
        private static IMetricsTelemetryCollector _metrics = NullMetricsTelemetryCollector.Instance;
        private static IConfigurationTelemetry _configuration = NullConfigurationTelemetry.Instance;

        private TelemetryFactory()
        {
        }

        public static TelemetryFactory Instance { get; } = new();

        public static IMetricsTelemetryCollector Metrics => Volatile.Read(ref _metrics);

        internal static IConfigurationTelemetry Config => Volatile.Read(ref _configuration);

        internal static IMetricsTelemetryCollector SetMetricsForTesting(IMetricsTelemetryCollector telemetry)
            => Interlocked.Exchange(ref _metrics, telemetry);

        internal static IConfigurationTelemetry SetConfigForTesting(IConfigurationTelemetry telemetry)
            => Interlocked.Exchange(ref _configuration, telemetry);

        public ITelemetryController CreateTelemetryController()
            => NullTelemetryController.Instance;
    }
}
