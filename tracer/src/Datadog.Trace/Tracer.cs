// <copyright file="Tracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.DogStatsd;
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
    public class Tracer : IDatadogTracer, IDatadogOpenTracingTracer
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
        /// Initializes a new instance of the <see cref="Tracer"/> class.
        /// For testing only.
        /// Note that this API does NOT replace the global Tracer instance.
        /// The <see cref="TracerManager"/> created will be scoped specifically to this instance.
        /// </summary>
        internal Tracer(TracerSettings settings, IAgentWriter agentWriter, ITraceSampler sampler, IScopeManager scopeManager, IStatsdManager statsd, ITelemetryController telemetry = null, IDiscoveryService discoveryService = null)
            : this(TracerManagerFactory.Instance.CreateTracerManager(settings, agentWriter, sampler, scopeManager, statsd, runtimeMetrics: null, logSubmissionManager: null, telemetry: telemetry ?? NullTelemetryController.Instance, discoveryService ?? NullDiscoveryService.Instance, dataStreamsManager: null, remoteConfigurationManager: null, dynamicConfigurationManager: null, tracerFlareManager: null, spanEventsManager: null))
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
        /// Gets the global <see cref="Tracer"/> instance.
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

                    // ensure Baggage's AsyncLocal<T> has a value as soon as we can,
                    // since it can only flow down the async call chain, not up
                    _ = Baggage.Current;
                }

                instance.TracerManager.Start();
                return instance;
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
        [MaybeNull]
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
        public string DefaultServiceName => CurrentTraceSettings.Settings.DefaultServiceName;

        /// <summary>
        /// Gets the git metadata provider.
        /// </summary>
        IGitMetadataTagsProvider IDatadogTracer.GitMetadataTagsProvider => TracerManager.GitMetadataTagsProvider;

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        public TracerSettings Settings => TracerManager.Settings;

        /// <summary>
        /// Gets the tracer's settings for the current trace.
        /// </summary>
        PerTraceSettings IDatadogTracer.PerTraceSettings => TracerManager.PerTraceSettings;

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
        internal static void Configure(TracerSettings settings)
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

        /// <summary>
        /// This creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <returns>A scope wrapping the newly created span</returns>
        [TestingOnly]
        public IScope StartActive(string operationName)
        {
#pragma warning disable DD0002 // Fine because this is also a testing only API
            return StartActive(operationName, default);
#pragma warning restore DD0002
        }

        /// <summary>
        /// This creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="settings">Settings for the new <see cref="IScope"/></param>
        /// <returns>A scope wrapping the newly created span</returns>
        [TestingOnly]
        public IScope StartActive(string operationName, SpanCreationSettings settings)
        {
            var spanContext = CreateSpanContext(
                operationName,
                resourceName: operationName,
                settings.Parent,
                serviceName: null);

            return spanContext switch
            {
                RecordedSpanContext recorded => StartActiveInternal(recorded, startTime: settings.StartTime, finishOnClose: settings.FinishOnClose ?? true),
                UnrecordedSpanContext unrecorded => StartActiveInternal(unrecorded, finishOnClose: settings.FinishOnClose ?? true),
                _ => null,
            };
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

            var spanContext = CreateSpanContext(
                operationName,
                resourceName: operationName,
                parent,
                serviceName: serviceName);

            return spanContext switch
            {
                RecordedSpanContext recorded => StartSpan(recorded, startTime: startTime),
                UnrecordedSpanContext unrecorded => null!,
                _ => null!,
            };
        }

        /// <summary>
        /// Forces the tracer to immediately flush pending traces and send them to the agent.
        /// To be called when the appdomain or the process is about to be killed in a non-graceful way.
        /// </summary>
        /// <returns>Task used to track the async flush operation</returns>
        public Task FlushAsync() => TracerManager.AgentWriter.FlushTracesAsync();

        /// <summary>
        /// Writes the specified <see cref="Span"/> collection to the agent writer.
        /// </summary>
        /// <param name="trace">The <see cref="Span"/> collection to write.</param>
        void IDatadogTracer.Write(ArraySegment<Span> trace)
        {
            if (CurrentTraceSettings.Settings.TraceEnabled || Settings.AzureAppServiceMetadata?.CustomTracingEnabled is true)
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

        internal MaybeRecordedSpanContext CreateSpanContext(string operationName, string resourceName, ISpanContext parent = null, string serviceName = null, TraceId traceId = default, ulong spanId = 0, string rawTraceId = null, string rawSpanId = null)
        {
            var spanContext = CreateInnerSpanContext(parent, serviceName, traceId, spanId, rawTraceId, rawSpanId);

            // make a sampling decision, to decide if we're going to
            // keep this span (and return a Span), or drop it (and return an UnrecordedSpan)
            int samplingPriority;
            if (spanContext.TraceContext.SamplingPriority is { } existingPriority)
            {
                // If we already have a sampling decision, honor it
                samplingPriority = existingPriority;
            }
            else
            {
                // We don't have a decision, so make one
                var samplingContext = new SamplingContext(spanContext, operationName, resourceName, tags: null);
                samplingPriority = spanContext.TraceContext.GetOrMakeSamplingDecision(in samplingContext);
            }

            return SamplingPriorityValues.IsKeep(samplingPriority)
                       ? new RecordedSpanContext(spanContext, operationName, resourceName)
                       : new UnrecordedSpanContext(spanContext, operationName, resourceName);
        }

        private SpanContext CreateInnerSpanContext(ISpanContext parent = null, string serviceName = null, TraceId traceId = default, ulong spanId = 0, string rawTraceId = null, string rawSpanId = null)
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

                    // When apm tracing is disabled, only distributed traces with the `_dd.p.ts` tag (with a trace source)
                    // are propagated downstream, however we need 1 trace per minute sent to the backend, so
                    // we unset sampling priority so the rate limiter decides.
                    if (Settings?.ApmTracingEnabled == false)
                    {
                        // If the trace has appsec propagation tag, the default priority is user keep
                        samplingPriority = propagatedTags.HasTraceSources(TraceSources.Asm) ? SamplingPriorityValues.UserKeep : null;
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
        internal Scope StartActiveInternal(RecordedSpanContext spanContext, DateTimeOffset? startTime = null, bool finishOnClose = true, ITags tags = null, IEnumerable<SpanLink> links = null)
        {
            var span = StartSpan(spanContext, tags, startTime, links: links);

            return TracerManager.ScopeManager.Activate(span, finishOnClose);
        }

        /// <remarks>
        /// When calling this method from an integration, ensure you call
        /// <c>Tracer.Instance.TracerManager.Telemetry.IntegrationGenerateSpan</c> so that the integration is recorded,
        /// and the span count metric is incremented. Alternatively, if this is not being called from an
        /// automatic integration, call <c>TelemetryFactory.Metrics.RecordCountSpanCreated()</c> directory instead.
        /// </remarks>
        internal Scope StartActiveInternal(UnrecordedSpanContext spanContext, DateTimeOffset? startTime = null, bool finishOnClose = true, ITags tags = null, IEnumerable<SpanLink> links = null)
        {
            var span = StartSpan(spanContext);

            return TracerManager.ScopeManager.Activate(span, finishOnClose);
        }

        /// <remarks>
        /// When calling this method from an integration, and _not_ discarding the span, ensure you call
        /// <c>Tracer.Instance.TracerManager.Telemetry.IntegrationGenerateSpan</c> so that the integration is recorded,
        /// and the span count metric is incremented. Alternatively, if this is not being called from an
        /// automatic integration, call <c>TelemetryFactory.Metrics.RecordCountSpanCreated()</c> directly instead.
        /// </remarks>
        internal Span StartSpan(RecordedSpanContext spanContext, ITags tags = null, DateTimeOffset? startTime = null, bool addToTraceContext = true, IEnumerable<SpanLink> links = null)
        {
            var span = new Span(spanContext, startTime, tags, links);

            // Apply any global tags
            if (CurrentTraceSettings.Settings.GlobalTags is { Count: > 0 } globalTags)
            {
                // if DD_TAGS contained "env", "version", "git.commit.sha", or "git.repository.url",  they were used to set
                // ImmutableTracerSettings.Environment, ImmutableTracerSettings.ServiceVersion, ImmutableTracerSettings.GitCommitSha, and ImmutableTracerSettings.GitRepositoryUrl
                // and removed from Settings.GlobalTags
                foreach (var entry in globalTags)
                {
                    span.SetTag(entry.Key, entry.Value);
                }
            }

            if (addToTraceContext)
            {
                spanContext.Context.TraceContext.AddSpan(span);
            }

            // Extract the Git metadata. This is done here because we may only be able to do it in the context of a request.
            // However, to reduce memory consumption, we don't actually add the result as tags on the span, and instead
            // write them directly to the <see cref="TraceChunkModel"/>.
            TracerManager.GitMetadataTagsProvider.TryExtractGitMetadata(out _);

            DebuggerManager.Instance.CodeOrigin?.SetCodeOriginForExitSpan(span);

            return span;
        }

        /// <remarks>
        /// When calling this method from an integration, and _not_ discarding the span, ensure you call
        /// <c>Tracer.Instance.TracerManager.Telemetry.IntegrationGenerateSpan</c> so that the integration is recorded,
        /// and the span count metric is incremented. Alternatively, if this is not being called from an
        /// automatic integration, call <c>TelemetryFactory.Metrics.RecordCountSpanCreated()</c> directly instead.
        /// </remarks>
        internal UnrecordedSpan StartSpan(UnrecordedSpanContext spanContext, bool addToTraceContext = true)
        {
            var span = new UnrecordedSpan(spanContext);

            if (addToTraceContext)
            {
                spanContext.Context.TraceContext.AddSpan(span);
            }

            return span;
        }
    }
}
