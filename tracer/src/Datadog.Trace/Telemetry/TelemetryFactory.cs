// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry.Collectors;
using Datadog.Trace.Telemetry.Transports;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetryFactory
    {
        // need to start collecting these immediately
        private static IMetricsTelemetryCollector _metrics = new MetricsTelemetryCollector();
        private static IConfigurationTelemetry _configurationV2 = new ConfigurationTelemetry();

        // V1 integration only
        private ConfigurationTelemetryCollector? _configuration;

        // v2 integration only
        private ProductsTelemetryCollector? _products;
        private ApplicationTelemetryCollectorV2? _application;

        // shared
        private IntegrationTelemetryCollector? _integrations;
        private IDependencyTelemetryCollector? _dependencies;

        private TelemetryFactory()
        {
        }

        public static TelemetryFactory Instance { get; } = new();

        /// <summary>
        /// Gets the static metrics instance used to record telemetry.
        /// </summary>
        public static IMetricsTelemetryCollector Metrics => Volatile.Read(ref _metrics);

        /// <summary>
        /// Gets the static configuration instance used to record telemetry
        /// </summary>
        internal static IConfigurationTelemetry Config => Volatile.Read(ref _configurationV2);

        internal static IMetricsTelemetryCollector SetMetricsForTesting(IMetricsTelemetryCollector telemetry)
            => Interlocked.Exchange(ref _metrics, telemetry);

        internal static IConfigurationTelemetry SetConfigForTesting(IConfigurationTelemetry telemetry)
            => Interlocked.Exchange(ref _configurationV2, telemetry);

        /// <summary>
        /// For testing purposes only. Use <see cref="Instance"/> in production
        /// </summary>
        public static TelemetryFactory CreateFactory() => new();

        public ITelemetryController CreateTelemetryController(ImmutableTracerSettings tracerSettings)
            => CreateTelemetryController(tracerSettings, TelemetrySettings.FromSource(GlobalConfigurationSource.Instance, Config));

        public ITelemetryController CreateTelemetryController(ImmutableTracerSettings tracerSettings, TelemetrySettings settings)
        {
            // Deliberately not a static field, because otherwise creates a circular dependency during startup
            var log = DatadogLogging.GetLoggerFor<TelemetryFactory>();
            if (settings.TelemetryEnabled)
            {
                try
                {
                    var telemetryTransports = TelemetryTransportFactory.Create(settings, tracerSettings.Exporter);

                    if (telemetryTransports.Length == 0)
                    {
                        log.Debug("Telemetry collection disabled: no available transports");
                        return NullTelemetryController.Instance;
                    }

                    LazyInitializer.EnsureInitialized(
                        ref _dependencies,
                        () => settings.DependencyCollectionEnabled
                                  ? new DependencyTelemetryCollector()
                                  : NullDependencyTelemetryCollector.Instance);

                    // we assume we never flip between v1 and v2
                    if (!settings.V2Enabled)
                    {
                        // if we're not using V2, we don't need the config collector
                        var oldConfig = Interlocked.Exchange(ref _configurationV2, NullConfigurationTelemetry.Instance);
                        if (oldConfig is ConfigurationTelemetry config)
                        {
                            config.Clear();
                        }
                    }

                    // if this changes, we will "lose" startup metrics, but unlikely to happen
                    if (!settings.MetricsEnabled)
                    {
                        // if we're not using metrics, we don't need the metrics collector
                        log.Debug("Telemetry metrics collection disabled");
                        var oldMetrics = Interlocked.Exchange(ref _metrics, NullMetricsTelemetryCollector.Instance);
                        if (oldMetrics is MetricsTelemetryCollector metrics)
                        {
                            // "clears" all the data stored so far
                            metrics.Clear();
                        }
                    }

                    // Making assumptions that we never switch from v1 to v2
                    // so we don't need to "clean up" the collectors.
                    if (settings.V2Enabled)
                    {
                        log.Debug("Creating telemetry controller v2");
                        return CreateV2Controller(telemetryTransports, settings);
                    }
                    else
                    {
                        log.Debug("Creating telemetry controller v1");
                        return CreateV1Controller(telemetryTransports, settings);
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Telemetry collection disabled: error initializing telemetry");
                    return NullTelemetryController.Instance;
                }
            }

            log.Debug("Telemetry collection disabled");
            return NullTelemetryController.Instance;
        }

        private ITelemetryController CreateV1Controller(
            ITelemetryTransport[] telemetryTransports,
            TelemetrySettings settings)
        {
            var transportManager = new TelemetryTransportManager(telemetryTransports);

            // Initialized once so if we create a new controller from this factory we get the same collector instances
            var configuration = LazyInitializer.EnsureInitialized(ref _configuration)!;
            var integrations = LazyInitializer.EnsureInitialized(ref _integrations)!;

            return new TelemetryController(
                configuration,
                _dependencies,
                integrations,
                transportManager,
                TelemetryConstants.DefaultFlushInterval,
                settings.HeartbeatInterval);
        }

        private ITelemetryController CreateV2Controller(
            ITelemetryTransport[] telemetryTransports,
            TelemetrySettings settings)
        {
            var transportManager = new TelemetryTransportManagerV2(telemetryTransports);
            // Initialized once so if we create a new controller from this factory we get the same collector instances
            var integrations = LazyInitializer.EnsureInitialized(ref _integrations)!;
            var products = LazyInitializer.EnsureInitialized(ref _products)!;
            var application = LazyInitializer.EnsureInitialized(ref _application)!;

            return new TelemetryControllerV2(
                Config,
                _dependencies!,
                integrations,
                Metrics,
                products,
                application,
                transportManager,
                settings.HeartbeatInterval);
        }
    }
}
