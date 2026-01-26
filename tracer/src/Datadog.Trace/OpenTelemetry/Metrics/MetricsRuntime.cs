// <copyright file="MetricsRuntime.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

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

            MetricExporter exporter = new OtlpExporter(settings);
            if (settings.StatsComputationEnabled)
            {
                var config = new ConfigurationBuilder(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
                var apiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString() ?? string.Empty;
                var ddSite = config.WithKeys(ConfigurationKeys.Site)
                                   .AsString(
                                       defaultValue: "datadoghq.com",
                                       validator: siteFromEnv => !string.IsNullOrEmpty(siteFromEnv));
                var statsUri = $"https://trace.agent.{ddSite}/api/v0.2/stats";

                if (!Uri.TryCreate(statsUri, UriKind.Absolute, out var uri))
                {
                    Log.Error($"The stats url was not a valid URL. No trace stats will be sent.");
                }
                else if (string.IsNullOrEmpty(apiKey))
                {
                    Log.Error("No API key found. No trace stats will be sent.");
                }
                else
                {
                    var headers = new Dictionary<string, string>
                    {
                        { "DD-Protocol", "otlp" },
                        { "DD-Api-Key", apiKey },
                        { "X-Datadog-Reported-Languages", "dotnet" },
                    };
                    exporter = new TraceStatsRoutingMetricExporter(settings, uri, Configuration.OtlpProtocol.HttpProtobuf, headers, exporter);
                }
            }

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

