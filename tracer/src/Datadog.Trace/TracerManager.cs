// <copyright file="TracerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace
{
    /// <summary>
    /// This class is responsible for managing the singleton objects associated with a Tracer.
    /// In normal usage, the <see cref="Instance"/> should be the only "live" instance. For testing
    /// purposes, we still need to create instances and keep them separate.
    /// </summary>
    internal class TracerManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerManager>();

        private static volatile bool _firstInitialization = true;
        private static Timer _heartbeatTimer;

        private static TracerManager _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        private volatile bool _isClosing = false;

        public TracerManager(
            ImmutableTracerSettings settings,
            IAgentWriter agentWriter,
            ISampler sampler,
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetricsWriter,
            DirectLogSubmissionManager directLogSubmission,
            string defaultServiceName)
        {
            Settings = settings;
            AgentWriter = agentWriter;
            Sampler = sampler;
            ScopeManager = scopeManager;
            Statsd = statsd;
            RuntimeMetrics = runtimeMetricsWriter;
            DefaultServiceName = defaultServiceName;
            DirectLogSubmission = directLogSubmission;
        }

        /// <summary>
        /// Gets the global <see cref="TracerManager"/> instance used by all <see cref="Tracer"/> instances
        /// </summary>
        public static TracerManager Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock,
                    () => CreateInitializedTracer(settings: null, TracerManagerFactory.Instance));
            }
        }

        /// <summary>
        /// Gets the default service name for traces where a service name is not specified.
        /// </summary>
        public string DefaultServiceName { get; }

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        public ImmutableTracerSettings Settings { get; }

        public IAgentWriter AgentWriter { get; }

        /// <summary>
        /// Gets the tracer's scope manager, which determines which span is currently active, if any.
        /// </summary>
        public IScopeManager ScopeManager { get; }

        /// <summary>
        /// Gets the <see cref="ISampler"/> instance used by this <see cref="IDatadogTracer"/> instance.
        /// </summary>
        public ISampler Sampler { get; }

        public DirectLogSubmissionManager DirectLogSubmission { get; }

        public IDogStatsd Statsd { get; }

        private RuntimeMetricsWriter RuntimeMetrics { get; }

        /// <summary>
        /// Replaces the global <see cref="TracerManager"/> settings. This affects all <see cref="Tracer"/> instances
        /// which use the global <see cref="TracerManager"/>
        /// </summary>
        /// <param name="settings">The settings to use </param>
        /// <param name="factory">The factory to use to create the <see cref="TracerManager"/></param>
        public static void ReplaceGlobalManager(ImmutableTracerSettings settings, TracerManagerFactory factory)
        {
            TracerManager oldManager;
            TracerManager newManager;
            lock (_globalInstanceLock)
            {
                oldManager = _instance;
                newManager = CreateInitializedTracer(settings, factory);
                _instance = newManager;
                _globalInstanceInitialized = true;
            }

            if (oldManager is not null)
            {
                // Clean up the old TracerManager instance
                oldManager._isClosing = true;
                // Fire and forget
                _ = CleanUpOldTracerManager(oldManager, newManager);
            }
        }

        /// <summary>
        /// Sets the global tracer instance without any validation or cleanup.
        /// Intended use is for unit testing only
        /// </summary>
        public static void UnsafeReplaceGlobalManager(TracerManager instance)
        {
            lock (_globalInstanceLock)
            {
                _instance = instance;
                _globalInstanceInitialized = true;
            }
        }

        /// <summary>
        /// Start internal processes that require Tracer.Instance is already set
        /// </summary>
        internal void Start()
        {
            // Must be idempotent and thread safe
            DirectLogSubmission?.Sink.Start();
        }

        private static async Task CleanUpOldTracerManager(TracerManager oldManager, TracerManager newManager)
        {
            try
            {
                var agentWriterReplaced = false;
                if (oldManager.AgentWriter != newManager.AgentWriter && oldManager.AgentWriter is not null)
                {
                    agentWriterReplaced = true;
                    await oldManager.AgentWriter.FlushAndCloseAsync().ConfigureAwait(false);
                }

                var statsdReplaced = false;
                if (oldManager.Statsd != newManager.Statsd)
                {
                    statsdReplaced = true;
                    oldManager.Statsd?.Dispose();
                }

                var runtimeMetricsWriterReplaced = false;
                if (oldManager.RuntimeMetrics != newManager.RuntimeMetrics)
                {
                    runtimeMetricsWriterReplaced = true;
                    oldManager.RuntimeMetrics?.Dispose();
                }

                Log.Information(
                    exception: null,
                    "Replaced global instances. AgentWriter: {AgentWriterReplaced}, StatsD: {StatsDReplaced}, RuntimeMetricsWriter: {RuntimeMetricsWriterReplaced}",
                    new object[] { agentWriterReplaced, statsdReplaced, runtimeMetricsWriterReplaced });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up old tracer manager");
            }
        }

        private static async Task WriteDiagnosticLog(TracerManager instance)
        {
            if (instance._isClosing)
            {
                return;
            }

            string agentError = null;

            // In AAS, the trace agent is deployed alongside the tracer and managed by the tracer
            // Disable this check as it may hit the trace agent before it is ready to receive requests and give false negatives
            if (!AzureAppServices.Metadata.IsRelevant)
            {
                try
                {
                    var success = await instance.AgentWriter.Ping().ConfigureAwait(false);

                    if (!success)
                    {
                        agentError = "An error occurred while sending traces to the agent";
                    }
                }
                catch (Exception ex)
                {
                    agentError = ex.Message;
                }
            }

            try
            {
                var instanceSettings = instance.Settings;
                var stringWriter = new StringWriter();

                using (var writer = new JsonTextWriter(stringWriter))
                {
                    // ReSharper disable MethodHasAsyncOverload
                    writer.WriteStartObject();

                    writer.WritePropertyName("date");
                    writer.WriteValue(DateTime.Now);

                    writer.WritePropertyName("os_name");
                    writer.WriteValue(FrameworkDescription.Instance.OSPlatform);

                    writer.WritePropertyName("os_version");
                    writer.WriteValue(Environment.OSVersion.ToString());

                    writer.WritePropertyName("version");
                    writer.WriteValue(TracerConstants.AssemblyVersion);

                    writer.WritePropertyName("platform");
                    writer.WriteValue(FrameworkDescription.Instance.ProcessArchitecture);

                    writer.WritePropertyName("lang");
                    writer.WriteValue(FrameworkDescription.Instance.Name);

                    writer.WritePropertyName("lang_version");
                    writer.WriteValue(FrameworkDescription.Instance.ProductVersion);

                    writer.WritePropertyName("env");
                    writer.WriteValue(instanceSettings.Environment);

                    writer.WritePropertyName("enabled");
                    writer.WriteValue(instanceSettings.TraceEnabled);

                    writer.WritePropertyName("service");
                    writer.WriteValue(instance.DefaultServiceName);

                    writer.WritePropertyName("agent_url");
                    writer.WriteValue(instanceSettings.Exporter.AgentUri);

                    writer.WritePropertyName("debug");
                    writer.WriteValue(GlobalSettings.Source.DebugEnabled);

                    writer.WritePropertyName("health_checks_enabled");
                    writer.WriteValue(instanceSettings.TracerMetricsEnabled);

#pragma warning disable 618 // App analytics is deprecated, but still used
                    writer.WritePropertyName("analytics_enabled");
                    writer.WriteValue(instanceSettings.AnalyticsEnabled);
#pragma warning restore 618

                    writer.WritePropertyName("sample_rate");
                    writer.WriteValue(instanceSettings.GlobalSamplingRate);

                    writer.WritePropertyName("sampling_rules");
                    writer.WriteValue(instanceSettings.CustomSamplingRules);

                    writer.WritePropertyName("tags");

                    writer.WriteStartArray();

                    foreach (var entry in instanceSettings.GlobalTags)
                    {
                        writer.WriteValue(string.Concat(entry.Key, ":", entry.Value));
                    }

                    writer.WriteEndArray();

                    writer.WritePropertyName("log_injection_enabled");
                    writer.WriteValue(instanceSettings.LogsInjectionEnabled);

                    writer.WritePropertyName("runtime_metrics_enabled");
                    writer.WriteValue(instanceSettings.RuntimeMetricsEnabled);

                    writer.WritePropertyName("disabled_integrations");
                    writer.WriteStartArray();

                    // In contrast to 1.x, this only shows _known_ integrations, but
                    // lists them whether they were explicitly disabled with
                    // DD_DISABLED_INTEGRATIONS, DD_TRACE_{0}_ENABLED, DD_{0}_ENABLED,
                    // or manually in code.
                    foreach (var integration in instanceSettings.Integrations.Settings)
                    {
                        if (integration.Enabled == false)
                        {
                            writer.WriteValue(integration.IntegrationName);
                        }
                    }

                    writer.WriteEndArray();

                    writer.WritePropertyName("routetemplate_resourcenames_enabled");
                    writer.WriteValue(instanceSettings.RouteTemplateResourceNamesEnabled);

                    writer.WritePropertyName("partialflush_enabled");
                    writer.WriteValue(instanceSettings.Exporter.PartialFlushEnabled);

                    writer.WritePropertyName("partialflush_minspans");
                    writer.WriteValue(instanceSettings.Exporter.PartialFlushMinSpans);

                    writer.WritePropertyName("runtime_id");
                    writer.WriteValue(Tracer.RuntimeId);

                    writer.WritePropertyName("agent_reachable");
                    writer.WriteValue(agentError == null);

                    writer.WritePropertyName("agent_error");
                    writer.WriteValue(agentError ?? string.Empty);

                    writer.WritePropertyName("appsec_enabled");
                    writer.WriteValue(Security.Instance.Settings.Enabled);

                    writer.WritePropertyName("appsec_trace_rate_limit");
                    writer.WriteValue(Security.Instance.Settings.TraceRateLimit);

                    writer.WritePropertyName("appsec_rules_file_path");
                    writer.WriteValue(Security.Instance.Settings.Rules ?? "(default)");

                    writer.WritePropertyName("appsec_libddwaf_version");
                    writer.WriteValue(Security.Instance.DdlibWafVersion?.ToString() ?? "(none)");

                    writer.WritePropertyName("direct_logs_submission_enabled_integrations");
                    writer.WriteStartArray();

                    foreach (var integration in instanceSettings.LogSubmissionSettings.EnabledIntegrationNames)
                    {
                        writer.WriteValue(integration);
                    }

                    writer.WriteEndArray();

                    writer.WritePropertyName("direct_logs_submission_enabled");
                    writer.WriteValue(instanceSettings.LogSubmissionSettings.IsEnabled);

                    writer.WritePropertyName("direct_logs_submission_error");
                    writer.WriteValue(string.Join(", ", instanceSettings.LogSubmissionSettings.ValidationErrors));

                    writer.WriteEndObject();
                    // ReSharper restore MethodHasAsyncOverload
                }

                Log.Information("DATADOG TRACER CONFIGURATION - {Configuration}", stringWriter.ToString());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DATADOG TRACER DIAGNOSTICS - Error fetching configuration");
            }
        }

        // should only be called inside a global lock, i.e. by TracerManager.Instance or ReplaceGlobalManager
        private static TracerManager CreateInitializedTracer(ImmutableTracerSettings settings, TracerManagerFactory factory)
        {
            if (_instance is ILockedTracer)
            {
                ThrowHelper.ThrowInvalidOperationException("The current tracer instance cannot be replaced.");
            }

            var newManager = factory.CreateTracerManager(settings, _instance);

            if (_firstInitialization)
            {
                _firstInitialization = false;
                OneTimeSetup();
            }

            if (newManager.Settings.StartupDiagnosticLogEnabled)
            {
                _ = Task.Run(() => WriteDiagnosticLog(newManager));
            }

            return newManager;
        }

        private static void OneTimeSetup()
        {
            // Register callbacks to make sure we flush the traces before exiting
            LifetimeManager.Instance.AddShutdownTask(RunShutdownTasks);

            // start the heartbeat loop
            _heartbeatTimer = new Timer(HeartbeatCallback, state: null, dueTime: TimeSpan.Zero, period: TimeSpan.FromMinutes(1));
        }

        private static void RunShutdownTasks()
        {
            try
            {
                _instance?.AgentWriter.FlushAndCloseAsync().Wait();
                _heartbeatTimer?.Dispose();
                _instance?.DirectLogSubmission?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error flushing traces on shutdown.");
            }
        }

        private static void HeartbeatCallback(object state)
        {
            // use the count of Tracer instances as the heartbeat value
            // to estimate the number of "live" Tracers than can potentially
            // send traces to the Agent
            _instance?.Statsd?.Gauge(TracerMetricNames.Health.Heartbeat, Tracer.LiveTracerCount);
        }
    }
}
