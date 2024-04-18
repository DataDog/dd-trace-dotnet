// <copyright file="TelemetryFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
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
        private static readonly Lazy<RedactedErrorLogCollector> _logs = new();
        private static IMetricsTelemetryCollector _metrics = new MetricsTelemetryCollector();
        private static IConfigurationTelemetry _configuration = new ConfigurationTelemetry();
        private readonly object _sync = new();

        private TelemetryController? _controller;

        // shared
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
        internal static IConfigurationTelemetry Config => Volatile.Read(ref _configuration);

        /// <summary>
        /// Gets the static log collector used to record redacted error logs
        /// </summary>
        internal static RedactedErrorLogCollector RedactedErrorLogs => _logs.Value;

        internal static IMetricsTelemetryCollector SetMetricsForTesting(IMetricsTelemetryCollector telemetry)
            => Interlocked.Exchange(ref _metrics, telemetry);

        internal static IConfigurationTelemetry SetConfigForTesting(IConfigurationTelemetry telemetry)
            => Interlocked.Exchange(ref _configuration, telemetry);

        /// <summary>
        /// For testing purposes only. Use <see cref="Instance"/> in production
        /// </summary>
        public static TelemetryFactory CreateFactory() => new();

        public ITelemetryController CreateTelemetryController(ImmutableTracerSettings tracerSettings, IDiscoveryService discoveryService)
            => CreateTelemetryController(tracerSettings, TelemetrySettings.FromSource(GlobalConfigurationSource.Instance, Config, tracerSettings, isAgentAvailable: null), discoveryService, useCiVisibilityTelemetry: false);

        public ITelemetryController CreateCiVisibilityTelemetryController(ImmutableTracerSettings tracerSettings, IDiscoveryService discoveryService, bool isAgentAvailable)
            => CreateTelemetryController(tracerSettings, TelemetrySettings.FromSource(GlobalConfigurationSource.Instance, Config, tracerSettings, isAgentAvailable), discoveryService, useCiVisibilityTelemetry: true);

        public ITelemetryController CreateTelemetryController(ImmutableTracerSettings tracerSettings, TelemetrySettings settings, IDiscoveryService discoveryService, bool useCiVisibilityTelemetry)
        {
            // Deliberately not a static field, because otherwise creates a circular dependency during startup
            var log = DatadogLogging.GetLoggerFor<TelemetryFactory>();

            // we assume telemetry can't switch between enabled/disabled because it can only be set via static config
            if (!settings.TelemetryEnabled)
            {
                log.Debug("Telemetry collection disabled");
                DisableTelemetry();
                return NullTelemetryController.Instance;
            }

            try
            {
                var telemetryTransports = TelemetryTransportFactory.Create(settings, tracerSettings.ExporterInternal);

                if (!telemetryTransports.HasTransports)
                {
                    log.Debug("Telemetry collection disabled: no available transports");
                    DisableTelemetry();
                    return NullTelemetryController.Instance;
                }

                LazyInitializer.EnsureInitialized(
                    ref _dependencies,
                    () => settings.DependencyCollectionEnabled
                              ? new DependencyTelemetryCollector()
                              : NullDependencyTelemetryCollector.Instance);

                // if this changes, we will "lose" startup metrics, but unlikely to happen
                if (!settings.MetricsEnabled)
                {
                    // if we're not using metrics, we don't need the metrics collector
                    log.Debug("Telemetry metrics collection disabled");
                    DisableMetricsCollector();
                }
                else if (useCiVisibilityTelemetry)
                {
                    log.Debug("CI Visibility telemetry metrics collection enabled");
                    // This would lose any metrics added up to this point
                    ReplaceMetricsCollector(new CiVisibilityMetricsTelemetryCollector());
                }

                log.Debug("Creating telemetry controller v2");
                return CreateController(telemetryTransports, settings, discoveryService);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Telemetry collection disabled: error initializing telemetry");
                DisableTelemetry();
                return NullTelemetryController.Instance;
            }
        }

        private static void DisableTelemetry()
        {
            DisableMetricsCollector();
            DisableConfigCollector();
            if (_logs.IsValueCreated)
            {
                // Logs were enabled, even though telemetry isn't
                _logs.Value.DisableCollector();
            }
        }

        private static void DisableMetricsCollector()
            => ReplaceMetricsCollector(NullMetricsTelemetryCollector.Instance);

        private static void ReplaceMetricsCollector(IMetricsTelemetryCollector newCollector)
        {
            var oldMetrics = Interlocked.Exchange(ref _metrics, newCollector);
            if (oldMetrics is MetricsTelemetryCollectorBase metrics)
            {
                // "clears" all the data stored so far
                metrics.Clear();
            }
        }

        private static void DisableConfigCollector()
        {
            var oldConfig = Interlocked.Exchange(ref _configuration, NullConfigurationTelemetry.Instance);
            if (oldConfig is ConfigurationTelemetry config)
            {
                // "clears" all the data stored so far
                config.Clear();
            }
        }

        private ITelemetryController CreateController(
            TelemetryTransports telemetryTransports,
            TelemetrySettings settings,
            IDiscoveryService discoveryService)
        {
            var transportManager = new TelemetryTransportManager(telemetryTransports, discoveryService);
            // The telemetry controller must be a singleton, so we initialize once
            // Note that any dependencies initialized inside the controller are also singletons (by design)
            // Initialized once so if we create a new controller from this factory we get the same collector instances.
            // (can't use LazyInitializer because that doesn't guarantee only a single instance is created,
            // and we start the task immediately)

            if (_controller is null)
            {
                lock (_sync)
                {
                    _controller ??= new TelemetryController(
                        Config,
                        _dependencies!,
                        Metrics,
                        _logs.IsValueCreated ? _logs.Value : null, // if we haven't created it by now, we don't need it
                        transportManager,
                        settings.HeartbeatInterval);
                }
            }

            _controller.DisableSending(); // disable sending until fully configured
            _controller.SetTransportManager(transportManager);
            _controller.SetFlushInterval(settings.HeartbeatInterval);

            return _controller;
        }
    }
}
