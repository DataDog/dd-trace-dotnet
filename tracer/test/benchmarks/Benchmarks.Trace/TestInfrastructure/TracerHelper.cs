using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;

using Datadog.Trace.Telemetry;

namespace Benchmarks.Trace;

public static class TracerHelper
{
    // Return a new instance each time to handle modification
    public static Dictionary<string, object> DefaultConfig => new()
    {
        { ConfigurationKeys.StartupDiagnosticLogEnabled, false },
        { ConfigurationKeys.Rcm.RemoteConfigurationEnabled, false },
        { ConfigurationKeys.AgentFeaturePollingEnabled, false },
        { ConfigurationKeys.Telemetry.Enabled, false },
    };

    public static Tracer CreateTracer(Dictionary<string, object> config = null)
        => CreateTracer(
            new TracerSettings(
                new DictionaryObjectConfigurationSource(config ?? DefaultConfig),
                NullConfigurationTelemetry.Instance,
                new OverrideErrorLog()));

    public static Tracer CreateTracer(TracerSettings settings)
    {
        // Using NullTelemetryController alone leaves the static metrics collector (and its
        // 10s aggregation loop) running, which adds per-process noise to benchmarks.
        // Install the null metrics collector and stop the previous collector's loop.
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(NullMetricsTelemetryCollector.Instance);
        if (previousMetrics is MetricsTelemetryCollectorBase metricsCollector)
        {
            metricsCollector.DisposeAsync().GetAwaiter().GetResult();
        }

        return new(settings, new DummyAgentWriter(), null, null, null, NullTelemetryController.Instance, NullDiscoveryService.Instance);
    }

    // Auto-instrumentation resolves Tracer.Instance; install the dummy tracer globally so
    // the benchmark doesn't lazily create a real tracer (real AgentWriter, discovery
    // service, remote config, heartbeat) that targets a non-existent agent
    public static Tracer CreateAndSetGlobalTracer(TracerSettings settings)
    {
        var tracer = CreateTracer(settings);
        Tracer.UnsafeSetTracerInstance(tracer);
        return tracer;
    }

    public static void SetGlobalTracer(Dictionary<string, object> config = null)
    {
        Tracer.UnsafeSetTracerInstance(CreateTracer(config));
    }

    public static void CleanupGlobalTracer()
    {
        Tracer.Instance.TracerManager.ShutdownAsync().GetAwaiter().GetResult();
        Tracer.UnsafeSetTracerInstance(null);
    }

    extension(TracerSettings)
    {
        internal static TracerSettings Create(Dictionary<string, object> settings) =>
            new(
                new DictionaryConfigurationSource(settings.ToDictionary(x => x.Key, x => FormattableString.Invariant($"{x.Value}"))),
                new ConfigurationTelemetry(),
                new OverrideErrorLog());
    }
}
