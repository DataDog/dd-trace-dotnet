// <copyright file="TracerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.Processors;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util.Http;
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
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetricsWriter,
            DirectLogSubmissionManager directLogSubmission,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            DataStreamsManager dataStreamsManager,
            string defaultServiceName,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IRemoteConfigurationManager remoteConfigurationManager,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ITraceProcessor[] traceProcessors = null)
        {
            Settings = settings;
            AgentWriter = agentWriter;
            ScopeManager = scopeManager;
            Statsd = statsd;
            RuntimeMetrics = runtimeMetricsWriter;
            DefaultServiceName = defaultServiceName;
            GitMetadataTagsProvider = gitMetadataTagsProvider;
            DataStreamsManager = dataStreamsManager;
            DirectLogSubmission = directLogSubmission;
            Telemetry = telemetry;
            DiscoveryService = discoveryService;
            TraceProcessors = traceProcessors ?? [];
            QueryStringManager = new(settings.QueryStringReportingEnabled, settings.ObfuscationQueryStringRegexTimeout, settings.QueryStringReportingSize, settings.ObfuscationQueryStringRegex);
            var lstTagProcessors = new List<ITagProcessor>(TraceProcessors.Length);
            foreach (var traceProcessor in TraceProcessors)
            {
                if (traceProcessor?.GetTagProcessor() is { } tagProcessor)
                {
                    lstTagProcessors.Add(tagProcessor);
                }
            }

            TagProcessors = lstTagProcessors.ToArray();

            RemoteConfigurationManager = remoteConfigurationManager;
            DynamicConfigurationManager = dynamicConfigurationManager;
            TracerFlareManager = tracerFlareManager;

            var schema = new NamingSchema(settings.MetadataSchemaVersion, settings.PeerServiceTagsEnabled, settings.RemoveClientServiceNamesEnabled, defaultServiceName, settings.ServiceNameMappings, settings.PeerServiceNameMappings);
            PerTraceSettings = new(traceSampler, spanSampler, settings.ServiceNameMappings, schema);
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

        public IGitMetadataTagsProvider GitMetadataTagsProvider { get; }

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        public ImmutableTracerSettings Settings { get; }

        public IAgentWriter AgentWriter { get; }

        /// <summary>
        /// Gets the tracer's scope manager, which determines which span is currently active, if any.
        /// </summary>
        public IScopeManager ScopeManager { get; }

        public DirectLogSubmissionManager DirectLogSubmission { get; }

        /// Gets the global <see cref="QueryStringManager"/> instance.
        public QueryStringManager QueryStringManager { get; }

        public IDogStatsd Statsd { get; }

        public ITraceProcessor[] TraceProcessors { get; }

        public ITagProcessor[] TagProcessors { get; }

        public ITelemetryController Telemetry { get; }

        public IDiscoveryService DiscoveryService { get; }

        public DataStreamsManager DataStreamsManager { get; }

        public IRemoteConfigurationManager RemoteConfigurationManager { get; }

        public IDynamicConfigurationManager DynamicConfigurationManager { get; }

        public ITracerFlareManager TracerFlareManager { get; }

        public RuntimeMetricsWriter RuntimeMetrics { get; }

        public PerTraceSettings PerTraceSettings { get; }

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
            // Start the Serverless Mini Agent in GCP Functions & Azure Consumption Plan Functions.
            ServerlessMiniAgent.StartServerlessMiniAgent(Settings);

            // Must be idempotent and thread safe
            DirectLogSubmission?.Sink.Start();
            Telemetry?.Start();
            DynamicConfigurationManager.Start();
            TracerFlareManager.Start();
            RemoteConfigurationManager.Start();
        }

        /// <summary>
        /// Internal for testing only.
        /// Run all the shutdown tasks for a standalone <see cref="TracerManager"/> instance,
        /// stopping the background processes.
        /// </summary>
        internal Task ShutdownAsync() => RunShutdownTasksAsync(this, null);

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

                var runtimeMetricsWriterReplaced = false;
                if (oldManager.RuntimeMetrics != newManager.RuntimeMetrics)
                {
                    runtimeMetricsWriterReplaced = true;
                    oldManager.RuntimeMetrics?.Dispose();
                }

                var statsdReplaced = false;
                if (oldManager.Statsd != newManager.Statsd)
                {
                    statsdReplaced = true;
                    oldManager.Statsd?.Dispose();
                }

                var discoveryReplaced = false;
                if (oldManager.DiscoveryService != newManager.DiscoveryService && oldManager.DiscoveryService is not null)
                {
                    discoveryReplaced = true;
                    await oldManager.DiscoveryService.DisposeAsync().ConfigureAwait(false);
                }

                var dataStreamsReplaced = false;
                if (oldManager.DataStreamsManager != newManager.DataStreamsManager && oldManager.DataStreamsManager is not null)
                {
                    dataStreamsReplaced = true;
                    await oldManager.DataStreamsManager.DisposeAsync().ConfigureAwait(false);
                }

                var configurationManagerReplaced = false;
                if (oldManager.RemoteConfigurationManager != newManager.RemoteConfigurationManager && oldManager.RemoteConfigurationManager is not null)
                {
                    configurationManagerReplaced = true;
                    oldManager.RemoteConfigurationManager.Dispose();
                }

                var dynamicConfigurationManagerReplaced = false;
                if (oldManager.DynamicConfigurationManager != newManager.DynamicConfigurationManager && oldManager.DynamicConfigurationManager is not null)
                {
                    dynamicConfigurationManagerReplaced = true;
                    oldManager.DynamicConfigurationManager.Dispose();
                }

                var tracerFlareManagerReplaced = false;
                if (oldManager.TracerFlareManager != newManager.TracerFlareManager && oldManager.TracerFlareManager is not null)
                {
                    tracerFlareManagerReplaced = true;
                    oldManager.TracerFlareManager.Dispose();
                }

                Log.Information(
                    exception: null,
                    "Replaced global instances. AgentWriter: {AgentWriterReplaced}, StatsD: {StatsDReplaced}, RuntimeMetricsWriter: {RuntimeMetricsWriterReplaced}, Discovery: {DiscoveryReplaced}, DataStreamsManager: {DataStreamsManagerReplaced}, RemoteConfigurationManager: {ConfigurationManagerReplaced}, DynamicConfigurationManager: {DynamicConfigurationManagerReplaced}, TracerFlareManager {TracerFlareManagerReplaced}",
                    new object[] { agentWriterReplaced, statsdReplaced, runtimeMetricsWriterReplaced, discoveryReplaced, dataStreamsReplaced, configurationManagerReplaced, dynamicConfigurationManagerReplaced, tracerFlareManagerReplaced });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up old tracer manager");
            }
        }

        private static async Task WriteDiagnosticLog(TracerManager instance)
        {
            try
            {
                if (instance._isClosing)
                {
                    return;
                }

                string agentError = null;
                var instanceSettings = instance.Settings;

                // In AAS, the trace agent is deployed alongside the tracer and managed by the tracer
                // Disable this check as it may hit the trace agent before it is ready to receive requests and give false negatives
                // Also disable if tracing is not enabled (as likely to be in an environment where agent is not available)
                if (instanceSettings.TraceEnabledInternal && !instanceSettings.IsRunningInAzureAppService)
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

                var stringWriter = new StringWriter();

                using (var writer = new JsonTextWriter(stringWriter))
                {
                    void WriteDictionary(IReadOnlyDictionary<string, string> dictionary)
                    {
                        writer.WriteStartArray();

                        if (dictionary is not null)
                        {
                            foreach (var kvp in dictionary)
                            {
                                writer.WriteValue($"{kvp.Key}:{kvp.Value}");
                            }
                        }

                        writer.WriteEndArray();
                    }

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

                    writer.WritePropertyName("native_tracer_version");
                    writer.WriteValue(Instrumentation.GetNativeTracerVersion());

                    writer.WritePropertyName("platform");
                    writer.WriteValue(FrameworkDescription.Instance.ProcessArchitecture);

                    writer.WritePropertyName("lang");
                    writer.WriteValue(FrameworkDescription.Instance.Name);

                    writer.WritePropertyName("lang_version");
                    writer.WriteValue(FrameworkDescription.Instance.ProductVersion);

                    writer.WritePropertyName("env");
                    writer.WriteValue(instanceSettings.EnvironmentInternal);

                    writer.WritePropertyName("enabled");
                    writer.WriteValue(instanceSettings.TraceEnabledInternal);

                    writer.WritePropertyName("service");
                    writer.WriteValue(instance.DefaultServiceName);

                    writer.WritePropertyName("agent_url");
                    writer.WriteValue(instanceSettings.ExporterInternal.AgentUriInternal);

                    writer.WritePropertyName("agent_transport");
                    writer.WriteValue(instanceSettings.ExporterInternal.TracesTransport.ToString());

                    writer.WritePropertyName("debug");
                    writer.WriteValue(GlobalSettings.Instance.DebugEnabledInternal);

                    writer.WritePropertyName("health_checks_enabled");
                    writer.WriteValue(instanceSettings.TracerMetricsEnabledInternal);

#pragma warning disable 618 // App analytics is deprecated, but still used
                    writer.WritePropertyName("analytics_enabled");
                    writer.WriteValue(instanceSettings.AnalyticsEnabledInternal);
#pragma warning restore 618

                    writer.WritePropertyName("sample_rate");
                    writer.WriteValue(instanceSettings.GlobalSamplingRateInternal);

                    writer.WritePropertyName("sampling_rules");
                    writer.WriteValue(instanceSettings.CustomSamplingRulesInternal);

                    writer.WritePropertyName("tags");
                    WriteDictionary(instanceSettings.GlobalTagsInternal);

                    writer.WritePropertyName("log_injection_enabled");
                    writer.WriteValue(instanceSettings.LogsInjectionEnabledInternal);

                    writer.WritePropertyName("runtime_metrics_enabled");
                    writer.WriteValue(instanceSettings.RuntimeMetricsEnabled);

                    writer.WritePropertyName("disabled_integrations");
                    writer.WriteStartArray();

                    // In contrast to 1.x, this only shows _known_ integrations, but
                    // lists them whether they were explicitly disabled with
                    // DD_DISABLED_INTEGRATIONS, DD_TRACE_{0}_ENABLED, DD_{0}_ENABLED,
                    // or manually in code.
                    foreach (var integration in instanceSettings.IntegrationsInternal.Settings)
                    {
                        if (integration.EnabledInternal == false)
                        {
                            writer.WriteValue(integration.IntegrationNameInternal);
                        }
                    }

                    writer.WriteEndArray();

                    writer.WritePropertyName("routetemplate_resourcenames_enabled");
                    writer.WriteValue(instanceSettings.RouteTemplateResourceNamesEnabled);

                    writer.WritePropertyName("routetemplate_expansion_enabled");
                    writer.WriteValue(instanceSettings.ExpandRouteTemplatesEnabled);

                    writer.WritePropertyName("querystring_reporting_enabled");
                    writer.WriteValue(instanceSettings.QueryStringReportingEnabled);

                    writer.WritePropertyName("obfuscation_querystring_regex_timeout");
                    writer.WriteValue(instanceSettings.ObfuscationQueryStringRegexTimeout);

                    writer.WritePropertyName("obfuscation_querystring_size");
                    writer.WriteValue(instanceSettings.QueryStringReportingSize);

                    if (string.Compare(instanceSettings.ObfuscationQueryStringRegex, TracerSettingsConstants.DefaultObfuscationQueryStringRegex, StringComparison.Ordinal) != 0)
                    {
                        writer.WritePropertyName("obfuscation_querystring_regex");
                        writer.WriteValue(instanceSettings.ObfuscationQueryStringRegex);
                    }

                    writer.WritePropertyName("partialflush_enabled");
                    writer.WriteValue(instanceSettings.ExporterInternal.PartialFlushEnabledInternal);

                    writer.WritePropertyName("partialflush_minspans");
                    writer.WriteValue(instanceSettings.ExporterInternal.PartialFlushMinSpansInternal);

                    writer.WritePropertyName("runtime_id");
                    writer.WriteValue(Tracer.RuntimeId);

                    writer.WritePropertyName("agent_reachable");
                    writer.WriteValue(agentError == null);

                    writer.WritePropertyName("agent_error");
                    writer.WriteValue(agentError ?? string.Empty);

                    WriteAsmInfo(writer);

                    writer.WritePropertyName("iast_enabled");
                    writer.WriteValue(Datadog.Trace.Iast.Iast.Instance.Settings.Enabled);

                    writer.WritePropertyName("iast_deduplication_enabled");
                    writer.WriteValue(Datadog.Trace.Iast.Iast.Instance.Settings.DeduplicationEnabled);

                    writer.WritePropertyName("iast_weak_hash_algorithms");
                    writer.WriteValue(Datadog.Trace.Iast.Iast.Instance.Settings.WeakHashAlgorithms);

                    writer.WritePropertyName("iast_weak_cipher_algorithms");
                    writer.WriteValue(Datadog.Trace.Iast.Iast.Instance.Settings.WeakCipherAlgorithms);

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

                    writer.WritePropertyName("exporter_settings_warning");
                    writer.WriteStartArray();

                    foreach (var warning in instanceSettings.ExporterInternal.ValidationWarnings)
                    {
                        writer.WriteValue(warning);
                    }

                    writer.WriteEndArray();

                    writer.WritePropertyName("dd_trace_methods");
                    writer.WriteValue(instanceSettings.TraceMethods);

                    writer.WritePropertyName("activity_listener_enabled");
                    writer.WriteValue(instanceSettings.IsActivityListenerEnabled);

                    writer.WritePropertyName("profiler_enabled");
                    writer.WriteValue(Profiler.Instance.Status.IsProfilerReady);

                    writer.WritePropertyName("code_hotspots_enabled");
                    writer.WriteValue(Profiler.Instance.ContextTracker.IsEnabled);

                    writer.WritePropertyName("wcf_obfuscation_enabled");
                    writer.WriteValue(instanceSettings.WcfObfuscationEnabled);

                    writer.WritePropertyName("data_streams_enabled");
                    writer.WriteValue(instanceSettings.IsDataStreamsMonitoringEnabled);

                    writer.WritePropertyName("span_sampling_rules");
                    writer.WriteValue(instanceSettings.SpanSamplingRules);

                    writer.WritePropertyName("stats_computation_enabled");
                    writer.WriteValue(instanceSettings.StatsComputationEnabledInternal);

                    writer.WritePropertyName("dbm_propagation_mode");
                    writer.WriteValue(instanceSettings.DbmPropagationMode.ToString());

                    writer.WritePropertyName("remote_configuration_available");
                    writer.WriteValue(instanceSettings.IsRemoteConfigurationAvailable);

                    writer.WritePropertyName("header_tags");
                    WriteDictionary(instanceSettings.HeaderTagsInternal);

                    writer.WritePropertyName("service_mapping");
                    WriteDictionary(instanceSettings.ServiceNameMappings);

                    writer.WritePropertyName("trace_propagation_style_extract_first_only");
                    writer.WriteValue(instanceSettings.PropagationExtractFirstOnly);

                    writer.WritePropertyName("trace_propagation_style_inject");
                    writer.WriteStartArray();

                    foreach (var warning in instanceSettings.PropagationStyleInject)
                    {
                        writer.WriteValue(warning);
                    }

                    writer.WriteEndArray();

                    writer.WritePropertyName("trace_propagation_style_extract");
                    writer.WriteStartArray();

                    foreach (var warning in instanceSettings.PropagationStyleExtract)
                    {
                        writer.WriteValue(warning);
                    }

                    writer.WriteEndArray();

                    writer.WriteEndObject();
                    // ReSharper restore MethodHasAsyncOverload
                }

                Log.Information("DATADOG TRACER CONFIGURATION - {Configuration}", stringWriter.ToString());

                OverrideErrorLog.Instance.ProcessAndClearActions(Log, TelemetryFactory.Metrics); // global errors, only logged once
                instanceSettings.ErrorLog.ProcessAndClearActions(Log, TelemetryFactory.Metrics); // global errors, only logged once
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "DATADOG TRACER DIAGNOSTICS - Error fetching configuration");
            }
        }

        private static void WriteAsmInfo(JsonTextWriter writer)
        {
            var security = Security.Instance;
            writer.WritePropertyName("appsec_enabled");
            writer.WriteValue(security.Settings.Enabled);

            if (security.Settings.ApiSecurityEnabled)
            {
                writer.WritePropertyName("appsec_apisecurity_enabled");
                writer.WriteValue(security.Settings.ApiSecurityEnabled);

                writer.WritePropertyName("appsec_apisecurity_sampling");
                writer.WriteValue(security.Settings.ApiSecuritySampling);
            }

            if (security.Settings.UseUnsafeEncoder)
            {
                writer.WritePropertyName("appsec_use_unsafe_encoder");
                writer.WriteValue(security.Settings.UseUnsafeEncoder);
            }

            writer.WritePropertyName("appsec_trace_rate_limit");
            writer.WriteValue(security.Settings.TraceRateLimit);

            writer.WritePropertyName("appsec_rules_file_path");
            writer.WriteValue(security.Settings.Rules ?? "(default)");

            writer.WritePropertyName("appsec_libddwaf_version");
            writer.WriteValue(security.DdlibWafVersion ?? "(none)");

            writer.WritePropertyName("dd_appsec_rasp_enabled");
            writer.WriteValue(security.Settings.RaspEnabled);

            writer.WritePropertyName("dd_appsec_stack_trace_enabled");
            writer.WriteValue(security.Settings.StackTraceEnabled);

            writer.WritePropertyName("dd_appsec_max_stack_traces");
            writer.WriteValue(security.Settings.MaxStackTraces);

            writer.WritePropertyName("dd_appsec_max_stack_trace_depth");
            writer.WriteValue(security.Settings.MaxStackTraceDepth);

            if (security.WafExportsErrorHappened)
            {
                writer.WritePropertyName("appsec_libddwaf_export_errors");
                writer.WriteValue(security.WafExportsErrorHappened);
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

            if (newManager.Settings.StartupDiagnosticLogEnabledInternal)
            {
                _ = Task.Run(() => WriteDiagnosticLog(newManager));
            }

            return newManager;
        }

        private static void OneTimeSetup()
        {
            // Register callbacks to make sure we flush the traces before exiting
            LifetimeManager.Instance.AddAsyncShutdownTask(RunShutdownTasksAsync);

            // start the heartbeat loop
            _heartbeatTimer = new Timer(HeartbeatCallback, state: null, dueTime: TimeSpan.Zero, period: TimeSpan.FromMinutes(1));
        }

        private static Task RunShutdownTasksAsync(Exception ex) => RunShutdownTasksAsync(_instance, _heartbeatTimer);

        private static async Task RunShutdownTasksAsync(TracerManager instance, Timer heartbeatTimer)
        {
            try
            {
                if (heartbeatTimer is not null)
                {
                    Log.Debug("Disposing Heartbeat timer.");
#if NETCOREAPP3_1_OR_GREATER
                    await heartbeatTimer.DisposeAsync().ConfigureAwait(false);
#else
                    heartbeatTimer.Dispose();
#endif
                }

                if (instance is not null)
                {
                    Log.Debug("Disposing DynamicConfigurationManager");
                    instance.DynamicConfigurationManager.Dispose();
                    Log.Debug("Disposing TracerFlareManager");
                    instance.TracerFlareManager.Dispose();

                    Log.Debug("Disposing AgentWriter.");
                    var flushTracesTask = instance.AgentWriter?.FlushAndCloseAsync() ?? Task.CompletedTask;
                    Log.Debug("Disposing DirectLogSubmission.");
                    var logSubmissionTask = instance.DirectLogSubmission?.DisposeAsync() ?? Task.CompletedTask;
                    Log.Debug("Disposing DiscoveryService.");
                    var discoveryService = instance.DiscoveryService?.DisposeAsync() ?? Task.CompletedTask;
                    Log.Debug("Disposing Data streams.");
                    var dataStreamsTask = instance.DataStreamsManager?.DisposeAsync() ?? Task.CompletedTask;
                    Log.Debug("Disposing RemoteConfigurationManager");
                    instance.RemoteConfigurationManager?.Dispose();

                    Log.Debug("Waiting for disposals.");
                    await Task.WhenAll(flushTracesTask, logSubmissionTask, discoveryService, dataStreamsTask).ConfigureAwait(false);

                    Log.Debug("Disposing Telemetry");
                    if (instance.Telemetry is { })
                    {
                        await instance.Telemetry.DisposeAsync().ConfigureAwait(false);
                    }

                    // We don't dispose runtime metrics on .NET Core because of https://github.com/dotnet/runtime/issues/103480
#if NETFRAMEWORK
                    Log.Debug("Disposing Runtime Metrics");
                    instance.RuntimeMetrics?.Dispose();
#endif

                    Log.Debug("Finished waiting for disposals.");
                }
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
            if (_instance?.Settings.TracerMetricsEnabledInternal == true)
            {
                _instance?.Statsd?.Gauge(TracerMetricNames.Health.Heartbeat, Tracer.LiveTracerCount);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTrace(ArraySegment<Span> trace)
        {
            foreach (var processor in TraceProcessors)
            {
                if (processor is not null)
                {
                    try
                    {
                        trace = processor.Process(trace);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error executing trace processor {TraceProcessorType}", processor?.GetType());
                    }
                }
            }

            if (trace.Count > 0)
            {
                AgentWriter.WriteTrace(trace);
            }
        }
    }
}
