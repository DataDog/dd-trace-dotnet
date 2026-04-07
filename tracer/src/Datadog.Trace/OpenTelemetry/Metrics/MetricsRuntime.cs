// <copyright file="MetricsRuntime.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OpenTelemetry.Metrics
{
    /// <summary>
    /// Static entry point for the OpenTelemetry Metrics pipeline.
    /// </summary>
    internal static class MetricsRuntime
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricsRuntime));
        private static OtelMetricsPipeline? _instance;

        public static void Start(TracerSettings settings)
        {
            if (_instance != null)
            {
                Log.Debug("MetricsRuntime already started");
                return;
            }

            var exporter = new OtlpExporter(settings, settings.Manager.InitialExporterSettings);
            _instance = new OtelMetricsPipeline(settings, exporter);
            _instance.Start();

            LifetimeManager.Instance.AddAsyncShutdownTask((_) => StopAsync());
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

            await _instance.StopAsync().ConfigureAwait(false);
        }
    }
}
#endif

