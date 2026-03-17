// <copyright file="MetricsRuntime.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.RuntimeMetrics;

namespace Datadog.Trace.OpenTelemetry.Metrics
{
    /// <summary>
    /// Static entry point for the OpenTelemetry Metrics pipeline.
    /// </summary>
    internal static class MetricsRuntime
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricsRuntime));
        private static readonly object StartLock = new();
        private static OtelMetricsPipeline? _instance;
        private static RuntimeMetricsPolyfill? _runtimeMetricsPolyfill;

        public static void Start(TracerSettings settings)
        {
            if (_instance != null)
            {
                return;
            }

            lock (StartLock)
            {
                if (_instance != null)
                {
                    Log.Debug("MetricsRuntime already started");
                    return;
                }

                var exporter = new OtlpExporter(settings);
                _instance = new OtelMetricsPipeline(settings, exporter);
                _instance.Start();

                if (Environment.Version.Major < 9)
                {
                    try
                    {
                        _runtimeMetricsPolyfill = new RuntimeMetricsPolyfill();
                        Log.Debug("Started RuntimeMetricsPolyfill for .NET {Version} (System.Runtime meter instruments not natively available until .NET 9)", Environment.Version);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to initialize RuntimeMetricsPolyfill for OTLP export");
                    }
                }

                LifetimeManager.Instance.AddAsyncShutdownTask((_) => StopAsync());
            }
        }

        public static Task ForceFlushAsync()
        {
            if (_instance == null)
            {
                return Task.CompletedTask;
            }

            return _instance.ForceCollectAndExportAsync();
        }

        public static async Task StopAsync()
        {
            if (_instance == null)
            {
                return;
            }

            _runtimeMetricsPolyfill?.Dispose();
            _runtimeMetricsPolyfill = null;

            await _instance.StopAsync().ConfigureAwait(false);
        }
    }
}
#endif

