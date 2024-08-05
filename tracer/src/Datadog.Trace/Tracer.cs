// <copyright file="Tracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace
{
    /// <summary>
    /// The tracer is responsible for creating spans and flushing them to the Datadog agent
    /// </summary>
    public class Tracer : ITracer, IDatadogTracer, IDatadogOpenTracingTracer
    {
        private static readonly object GlobalInstanceLock = new();

        /// <summary>
        /// The number of Tracer instances that have been created and not yet destroyed.
        /// This is used in the heartbeat metrics to estimate the number of
        /// "live" Tracers that could potentially be sending traces to the Agent.
        /// </summary>
        private static int _liveTracerCount;

        private static Tracer _instance;
        private static volatile bool _globalInstanceInitialized;

        private readonly TracerManager _tracerManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/> class with default settings. Replaces the
        /// settings for all tracers in the application with the default settings.
        /// </summary>
        [Obsolete("This API is deprecated. Use Tracer.Instance to obtain a Tracer instance to create spans.")]
        [PublicApi]
        public Tracer()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_Ctor);
            // Don't call Configure because it will call Start on the TracerManager
            // before this new instance of Tracer is assigned to Tracer.Instance
            TracerManager.ReplaceGlobalManager(null, TracerManagerFactory.Instance);

            // update the count of Tracer instances
            Interlocked.Increment(ref _liveTracerCount);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/>
        /// class using the specified <see cref="IConfigurationSource"/>. This constructor updates the global settings
        /// for all <see cref="Tracer"/> instances in the application.
        /// </summary>
        /// <param name="settings">
        /// A <see cref="TracerSettings"/> instance with the desired settings,
        /// or null to use the default configuration sources. This is used to configure global settings
        /// </param>
        [Obsolete("This API is deprecated, as it replaces the global settings for all Tracer instances in the application. " +
                  "If you were using this API to configure the global Tracer.Instance in code, use the static "
                + nameof(Tracer) + "." + nameof(Configure) + "() to replace the global Tracer settings for the application")]
        [PublicApi]
        public Tracer(TracerSettings settings)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_Ctor_Settings);
            // Don't call Configure because it will call Start on the TracerManager
            // before this new instance of Tracer is assigned to Tracer.Instance
            TracerManager.ReplaceGlobalManager(settings is null ? null : new ImmutableTracerSettings(settings, true), TracerManagerFactory.Instance);

            // update the count of Tracer instances
            Interlocked.Increment(ref _liveTracerCount);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/> class.
        /// For testing only.
        /// Note that this API does NOT replace the global Tracer instance.
        /// The <see cref="TracerManager"/> created will be scoped specifically to this instance.
        /// </summary>
        internal Tracer(TracerSettings settings, IAgentWriter agentWriter, ITraceSampler sampler, IScopeManager scopeManager, IDogStatsd statsd, ITelemetryController telemetry = null, IDiscoveryService discoveryService = null)
            : this(TracerManagerFactory.Instance.CreateTracerManager(settings is null ? null : new ImmutableTracerSettings(settings, true), agentWriter, sampler, scopeManager, statsd, runtimeMetrics: null, logSubmissionManager: null, telemetry: telemetry ?? NullTelemetryController.Instance, discoveryService ?? NullDiscoveryService.Instance, dataStreamsManager: null, remoteConfigurationManager: null, dynamicConfigurationManager: null, tracerFlareManager: null))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/> class.
        /// Should only be called DIRECTLY for testing purposes.
        /// If non-null the provided <see cref="TracerManager"/> will be tied to this TracerInstance (for testing purposes only)
        /// If null, the global <see cref="TracerManager"/> will be fetched or created, but will not be modified.
        /// </summary>
        private protected Tracer(TracerManager tracerManager)
        {
            _tracerManager = tracerManager;
            if (tracerManager is null)
            {
                // Ensure the global TracerManager instance has been created
                // to kick start background processes etc
                _ = TracerManager.Instance;
            }

            // update the count of Tracer instances
            Interlocked.Increment(ref _liveTracerCount);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Tracer"/> class.
        /// </summary>
        ~Tracer()
        {
            // update the count of Tracer instances
            Interlocked.Decrement(ref _liveTracerCount);
        }

        /// <summary>
        /// Gets or sets the global <see cref="Tracer"/> instance.
        /// Used by all automatic instrumentation and recommended
        /// as the entry point for manual instrumentation.
        /// </summary>
        public static Tracer Instance
        {
            get
            {
                if (_globalInstanceInitialized)
                {
                    return _instance;
                }

                Tracer instance;
                lock (GlobalInstanceLock)
                {
                    if (_globalInstanceInitialized)
                    {
                        return _instance;
                    }

                    instance = new Tracer(tracerManager: null); // don't replace settings, use existing
                    _instance = instance;
                    _globalInstanceInitialized = true;
                }

                instance.TracerManager.Start();
                return instance;
            }

            // TODO: Make this API internal
            [Obsolete("Use " + nameof(Tracer) + "." + nameof(Configure) + " to configure the global Tracer" +
                      " instance in code.")]
            [PublicApi]
            set
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_Instance_Set);
                if (value is null)
                {
                    ThrowHelper.ThrowArgumentNullException("The tracer instance shouldn't be set to null as this will cause issues with automatic instrumentation.");
                }

                lock (GlobalInstanceLock)
                {
                    // This check is probably no longer necessary, as it's the TracerManager we really care about
                    // Kept for safety reasons
                    if (_instance is { TracerManager: ILockedTracer })
                    {
                        ThrowHelper.ThrowInvalidOperationException("The current tracer instance cannot be replaced.");
                    }

                    _instance = value;
                    _globalInstanceInitialized = true;
                }

                value?.TracerManager.Start();
            }
        }

        /// <summary>
        /// Gets the active scope
        /// </summary>
        public IScope ActiveScope
        {
            get
            {
                return DistributedTracer.Instance.GetActiveScope() ?? InternalActiveScope;
            }
        }

        /// <summary>
        /// Gets the active span context dictionary by consulting DistributedTracer.Instance
        /// </summary>
        internal IReadOnlyDictionary<string, string> DistributedSpanContext => DistributedTracer.Instance.GetSpanContextRaw() ?? InternalActiveScope?.Span?.Context;

        /// <summary>
        /// Gets the active scope
        /// </summary>
        internal Scope InternalActiveScope => TracerManager.ScopeManager.Active;

        /// <summary>
        /// Gets the tracer's scope manager, which determines which span is currently active, if any.
        /// </summary>
        internal IScopeManager ScopeManager => TracerManager.ScopeManager;

        /// <summary>
        /// Gets the default service name for traces where a service name is not specified.
        /// </summary>
        public string DefaultServiceName => TracerManager.DefaultServiceName;

        /// <summary>
        /// Gets the git metadata provider.
        /// </summary>
        IGitMetadataTagsProvider IDatadogTracer.GitMetadataTagsProvider => TracerManager.GitMetadataTagsProvider;

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        public ImmutableTracerSettings Settings => TracerManager.Settings;

        /// <summary>
        /// Gets the tracer's settings for the current trace.
        /// </summary>
        PerTraceSettings IDatadogTracer.PerTraceSettings => TracerManager.PerTraceSettings;

        /// <summary>
        /// Gets the active scope
        /// </summary>
        IScope ITracer.ActiveScope => ActiveScope;

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        ImmutableTracerSettings ITracer.Settings => Settings;

        internal static string RuntimeId => DistributedTracer.Instance.GetRuntimeId();

        internal static int LiveTracerCount => _liveTracerCount;

        internal TracerManager TracerManager => _tracerManager ?? TracerManager.Instance;

        internal PerTraceSettings CurrentTraceSettings
        {
            get
            {
                if (InternalActiveScope?.Span?.Context?.TraceContext is { } context)
                {
                    return context.CurrentTraceSettings;
                }

                return TracerManager.PerTraceSettings;
            }
        }

        /// <summary>
        /// Replaces the global Tracer settings used by all <see cref="Tracer"/> instances,
        /// including automatic instrumentation
        /// </summary>
        /// <param name="settings"> A <see cref="TracerSettings"/> instance with the desired settings,
        /// or null to use the default configuration sources. This is used to configure global settings</param>
        [PublicApi]
        public static void Configure(TracerSettings settings)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_Configure);
            ConfigureInternal(settings is null ? null : new ImmutableTracerSettings(settings, true));
        }

        internal static void ConfigureInternal(ImmutableTracerSettings settings)
        {
            TracerManager.ReplaceGlobalManager(settings, TracerManagerFactory.Instance);
            Tracer.Instance.TracerManager.Start();
        }

        /// <summary>
        /// Sets the global tracer instance without any validation.
        /// Intended use is for unit testing
        /// </summary>
        /// <param name="instance">Tracer instance</param>
        internal static void UnsafeSetTracerInstance(Tracer instance)
        {
            lock (GlobalInstanceLock)
            {
                _instance = instance;
                _globalInstanceInitialized = true;
            }

            instance?.TracerManager.Start();
        }

        /// <inheritdoc cref="ITracer" />
        [PublicApi]
        IScope ITracer.StartActive(string operationName)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ITracer_StartActive);
            TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.Manual);
            return StartActiveInternal(operationName);
        }

        /// <inheritdoc cref="ITracer" />
        [PublicApi]
        IScope ITracer.StartActive(string operationName, SpanCreationSettings settings)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.ITracer_StartActive_Settings);
            TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.Manual);
            var finishOnClose = settings.FinishOnClose ?? true;
            return StartActiveInternal(operationName, settings.Parent, serviceName: null, settings.StartTime, finishOnClose);
        }

        /// <summary>
        /// This creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <returns>A scope wrapping the newly created span</returns>
        [PublicApi]
        public IScope StartActive(string operationName)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_StartActive);
            TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.Manual);
            return StartActiveInternal(operationName);
        }

        /// <summary>
        /// This creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="settings">Settings for the new <see cref="IScope"/></param>
        /// <returns>A scope wrapping the newly created span</returns>
        [PublicApi]
        public IScope StartActive(string operationName, SpanCreationSettings settings)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_StartActive_Settings);
            TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.Manual);
            var finishOnClose = settings.FinishOnClose ?? true;
            return StartActiveInternal(operationName, settings.Parent, serviceName: null, settings.StartTime, finishOnClose);
        }

        /// <summary>
        /// Creates a new <see cref="ISpan"/> with the specified parameters.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="parent">The span's parent</param>
        /// <param name="serviceName">The span's service name</param>
        /// <param name="startTime">An explicit start time for that span</param>
        /// <param name="ignoreActiveScope">If set the span will not be a child of the currently active span</param>
        /// <returns>The newly created span</returns>
        ISpan IDatadogOpenTracingTracer.StartSpan(string operationName, ISpanContext parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope)
        {
            TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.OpenTracing);
            if (ignoreActiveScope && parent == null)
            {
                // don't set the span's parent,
                // even if there is an active span
                parent = SpanContext.None;
            }

            var span = StartSpan(operationName, tags: null, parent, serviceName: null, startTime);

            if (serviceName != null)
            {
                // if specified, override the default service name
                span.ServiceName = serviceName;
            }

            return span;
        }

        /// <summary>
        /// Forces the tracer to immediately flush pending traces and send them to the agent.
        /// To be called when the appdomain or the process is about to be killed in a non-graceful way.
        /// </summary>
        /// <returns>Task used to track the async flush operation</returns>
        [PublicApi]
        public Task ForceFlushAsync()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.Tracer_ForceFlushAsync);
            return FlushAsync();
        }

        /// <summary>
        /// Writes the specified <see cref="Span"/> collection to the agent writer.
        /// </summary>
        /// <param name="trace">The <see cref="Span"/> collection to write.</param>
        void IDatadogTracer.Write(ArraySegment<Span> trace)
        {
            if (Settings.TraceEnabledInternal || Settings.AzureAppServiceMetadata?.CustomTracingEnabled is true)
            {
                TracerManager.WriteTrace(trace);
            }
        }

        /// <summary>
        /// Make a span the active span and return its new scope.
        /// </summary>
        /// <param name="span">The span to activate.</param>
        /// <param name="finishOnClose">Determines whether closing the returned scope will also finish the span.</param>
        /// <returns>A Scope object wrapping this span.</returns>
        internal Scope ActivateSpan(Span span, bool finishOnClose = true)
        {
            return TracerManager.ScopeManager.Activate(span, finishOnClose);
        }

        internal SpanContext CreateSpanContext(ISpanContext parent = null, string serviceName = null, TraceId traceId = default, ulong spanId = 0, string rawTraceId = null, string rawSpanId = null)
        {
            // null parent means use the currently active span
            parent ??= DistributedTracer.Instance.GetSpanContext() ?? TracerManager.ScopeManager.Active?.Span?.Context;
            string lastParentId = null;
            TraceContext traceContext;

            if (parent is SpanContext parentSpanContext)
            {
                // if the parent's TraceContext is not null, parent is a local span
                // and the new span we are creating belongs in the same TraceContext
                traceContext = parentSpanContext.TraceContext;

                if (traceContext == null)
                {
                    var propagatedTags = parentSpanContext.PropagatedTags;
                    var samplingPriority = parentSpanContext.SamplingPriority;

                    // When in appsec standalone mode, only distributed traces with the `_dd.p.appsec` tag
                    // are propagated downstream, however we need 1 trace per minute sent to the backend, so
                    // we unset sampling priority so the rate limiter decides.
                    if (Settings?.AppsecStandaloneEnabledInternal == true)
                    {
                        // If the trace has appsec propagation tag, the default priority is user keep
                        samplingPriority = propagatedTags?.GetTag(Tags.Propagated.AppSec) == "1" ? SamplingPriorityValues.UserKeep : null;
                    }

                    // If parent is SpanContext but its TraceContext is null, then it was extracted from propagation headers.
                    // Create a new TraceContext (this will start a new trace) and initialize
                    // it with the propagated values (sampling priority, origin, tags, W3C trace state, etc).
                    traceContext = new TraceContext(this, propagatedTags);
                    TelemetryFactory.Metrics.RecordCountTraceSegmentCreated(MetricTags.TraceContinuation.Continued);

                    samplingPriority ??= DistributedTracer.Instance.GetSamplingPriority();
                    traceContext.SetSamplingPriority(samplingPriority);
                    traceContext.Origin = parentSpanContext.Origin;
                    traceContext.AdditionalW3CTraceState = parentSpanContext.AdditionalW3CTraceState;
                }

                // if the parent is a remote context, set the last parent id that came from the distributed header
                // note that parentSpanContext.LastParent may be null
                if (parentSpanContext.IsRemote)
                {
                    lastParentId = parentSpanContext.LastParentId;
                }
            }
            else
            {
                // if parent is not a SpanContext, it must be either a ReadOnlySpanContext,
                // a user-defined ISpanContext implementation, or null (no parent). we don't have a TraceContext,
                // so create a new one (this will start a new trace).
                traceContext = new TraceContext(this, tags: null);
                TelemetryFactory.Metrics.RecordCountTraceSegmentCreated(MetricTags.TraceContinuation.New);

                // in a version-mismatch scenario, try to get the sampling priority from the "other" tracer
                var samplingPriority = DistributedTracer.Instance.GetSamplingPriority();
                traceContext.SetSamplingPriority(samplingPriority);

                if (traceId == TraceId.Zero &&
                    Activity.ActivityListener.GetCurrentActivity() is Activity.DuckTypes.IW3CActivity { TraceId: { } activityTraceId } activity)
                {
                    bool useActivityTraceId = true;

                    // if the ignore handler _should_ listen to an activity, that activity _should_ be ignored
                    if (activity is Activity.DuckTypes.IActivity5 activity5 && ActivityHandlersRegister.IgnoreHandler.ShouldListenTo(activity5.Source.Name, activity5.Source.Version))
                    {
                        // if the activity was ignored, we don't want to use its traceID as it'd create orphaned spans in the traces
                        useActivityTraceId = false;
                    }

                    if (useActivityTraceId)
                    {
                        // if there's an existing Activity we try to use its TraceId,
                        // but if Activity.IdFormat is not ActivityIdFormat.W3C, it may be null or unparsable
                        rawTraceId = activityTraceId;
                        HexString.TryParseTraceId(activityTraceId, out traceId);
                    }
                }
            }

            var finalServiceName = serviceName ?? DefaultServiceName;

            if (traceId == TraceId.Zero)
            {
                // generate the trace id here using the 128-bit setting
                // instead of letting the SpanContext generate it in its ctor
                var useAllBits = Settings?.TraceId128BitGenerationEnabled ?? true;
                traceId = RandomIdGenerator.Shared.NextTraceId(useAllBits);
            }

            var context = new SpanContext(parent, traceContext, finalServiceName, traceId: traceId, spanId: spanId, rawTraceId: rawTraceId, rawSpanId: rawSpanId);
            context.LastParentId = lastParentId; // lastParentId is only non-null when parent is extracted from W3C headers
            return context;
        }

        /// <remarks>
        /// When calling this method from an integration, ensure you call
        /// <c>Tracer.Instance.TracerManager.Telemetry.IntegrationGenerateSpan</c> so that the integration is recorded,
        /// and the span count metric is incremented. Alternatively, if this is not being called from an
        /// automatic integration, call <c>TelemetryFactory.Metrics.RecordCountSpanCreated()</c> directory instead.
        /// </remarks>
        internal Scope StartActiveInternal(string operationName, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool finishOnClose = true, ITags tags = null)
        {
            var span = StartSpan(operationName, tags, parent, serviceName, startTime);
            return TracerManager.ScopeManager.Activate(span, finishOnClose);
        }

        /// <remarks>
        /// When calling this method from an integration, and _not_ discarding the span, ensure you call
        /// <c>Tracer.Instance.TracerManager.Telemetry.IntegrationGenerateSpan</c> so that the integration is recorded,
        /// and the span count metric is incremented. Alternatively, if this is not being called from an
        /// automatic integration, call <c>TelemetryFactory.Metrics.RecordCountSpanCreated()</c> directly instead.
        /// </remarks>
        internal Span StartSpan(string operationName, ITags tags = null, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, TraceId traceId = default, ulong spanId = 0, string rawTraceId = null, string rawSpanId = null, bool addToTraceContext = true)
        {
            var spanContext = CreateSpanContext(parent, serviceName, traceId, spanId, rawTraceId, rawSpanId);

            var span = new Span(spanContext, startTime, tags)
            {
                OperationName = operationName,
            };

            // Apply any global tags
            if (Settings.GlobalTagsInternal.Count > 0)
            {
                // if DD_TAGS contained "env", "version", "git.commit.sha", or "git.repository.url",  they were used to set
                // ImmutableTracerSettings.Environment, ImmutableTracerSettings.ServiceVersion, ImmutableTracerSettings.GitCommitSha, and ImmutableTracerSettings.GitRepositoryUrl
                // and removed from Settings.GlobalTags
                foreach (var entry in Settings.GlobalTagsInternal)
                {
                    span.SetTag(entry.Key, entry.Value);
                }
            }

            if (addToTraceContext)
            {
                spanContext.TraceContext.AddSpan(span);
            }

            // Extract the Git metadata. This is done here because we may only be able to do it in the context of a request.
            // However, to reduce memory consumption, we don't actually add the result as tags on the span, and instead
            // write them directly to the <see cref="TraceChunkModel"/>.
            TracerManager.GitMetadataTagsProvider.TryExtractGitMetadata(out _);

            return span;
        }

        internal Task FlushAsync()
        {
            return TracerManager.AgentWriter.FlushTracesAsync();
        }
    }
}
