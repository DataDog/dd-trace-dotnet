// <copyright file="Tracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
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
        public Tracer()
            : this(settings: null)
        {
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
        public Tracer(TracerSettings settings)
        {
            // TODO: Switch to immutable settings
            Configure(settings);

            // update the count of Tracer instances
            Interlocked.Increment(ref _liveTracerCount);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracer"/> class.
        /// For testing only.
        /// Note that this API does NOT replace the global Tracer instance.
        /// The <see cref="TracerManager"/> created will be scoped specifically to this instance.
        /// </summary>
        internal Tracer(TracerSettings settings, IAgentWriter agentWriter, ISampler sampler, IScopeManager scopeManager, IDogStatsd statsd, ITelemetryController telemetry = null)
            : this(TracerManagerFactory.Instance.CreateTracerManager(settings?.Build(), agentWriter, sampler, scopeManager, statsd, runtimeMetrics: null, logSubmissionManager: null, telemetry: telemetry ?? NullTelemetryController.Instance))
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
            set
            {
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
        /// Gets this tracer's settings.
        /// </summary>
        public ImmutableTracerSettings Settings => TracerManager.Settings;

        /// <summary>
        /// Gets the active scope
        /// </summary>
        IScope ITracer.ActiveScope => ActiveScope;

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        ImmutableTracerSettings ITracer.Settings => Settings;

        /// <summary>
        /// Gets the <see cref="ISampler"/> instance used by this <see cref="IDatadogTracer"/> instance.
        /// </summary>
        ISampler IDatadogTracer.Sampler => TracerManager.Sampler;

        internal static string RuntimeId => DistributedTracer.Instance.GetRuntimeId();

        internal static int LiveTracerCount => _liveTracerCount;

        internal TracerManager TracerManager => _tracerManager ?? TracerManager.Instance;

        /// <summary>
        /// Replaces the global Tracer settings used by all <see cref="Tracer"/> instances,
        /// including automatic instrumentation
        /// </summary>
        /// <param name="settings"> A <see cref="TracerSettings"/> instance with the desired settings,
        /// or null to use the default configuration sources. This is used to configure global settings</param>
        public static void Configure(TracerSettings settings)
        {
            TracerManager.ReplaceGlobalManager(settings?.Build(), TracerManagerFactory.Instance);
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
        IScope ITracer.StartActive(string operationName) => StartActive(operationName);

        /// <inheritdoc cref="ITracer" />
        IScope ITracer.StartActive(string operationName, SpanCreationSettings settings) => StartActive(operationName, settings);

        /// <summary>
        /// This creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <returns>A scope wrapping the newly created span</returns>
        public IScope StartActive(string operationName)
        {
            return StartActiveInternal(operationName);
        }

        /// <summary>
        /// This creates a new span with the given parameters and makes it active.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="settings">Settings for the new <see cref="IScope"/></param>
        /// <returns>A scope wrapping the newly created span</returns>
        public IScope StartActive(string operationName, SpanCreationSettings settings)
        {
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
        public Task ForceFlushAsync() => FlushAsync();

        /// <summary>
        /// Writes the specified <see cref="Span"/> collection to the agent writer.
        /// </summary>
        /// <param name="trace">The <see cref="Span"/> collection to write.</param>
        void IDatadogTracer.Write(ArraySegment<Span> trace)
        {
            if (Settings.TraceEnabled || AzureAppServices.Metadata.CustomTracingEnabled)
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

        internal SpanContext CreateSpanContext(ISpanContext parent = null, string serviceName = null, ulong? traceId = null, ulong? spanId = null, string rawTraceId = null, string rawSpanId = null)
        {
            // null parent means use the currently active span
            parent ??= DistributedTracer.Instance.GetSpanContext() ?? TracerManager.ScopeManager.Active?.Span?.Context;

            TraceContext traceContext;

            // try to get the trace context (from local spans),
            // otherwise start a new trace context and get sampling priority (from propagated spans)
            if (parent is SpanContext parentSpanContext)
            {
                // if traceContext is not null, parent is from a local (non-propagated) span
                // and this child span belongs in the same TraceContext
                traceContext = parentSpanContext.TraceContext;

                if (traceContext == null)
                {
                    // if traceContext is null, parent was extracted from propagation headers.
                    // start a new trace and keep the sampling priority and trace tags.
                    var traceTags = TagPropagation.ParseHeader(parentSpanContext.PropagatedTags);
                    traceContext = new TraceContext(this, traceTags);

                    var samplingPriority = parentSpanContext.SamplingPriority ?? DistributedTracer.Instance.GetSamplingPriority();
                    traceContext.SetSamplingDecision(samplingPriority);
                }
            }
            else
            {
                // parent is not a SpanContext, start a new trace
                var samplingPriority = DistributedTracer.Instance.GetSamplingPriority();

                traceContext = new TraceContext(this, tags: null);
                traceContext.SetSamplingDecision(samplingPriority);

                if (traceId == null)
                {
                    var activity = Activity.ActivityListener.GetCurrentActivity();
                    if (activity is Activity.DuckTypes.IW3CActivity w3CActivity)
                    {
                        // If there's an existing activity we use the same traceId (converted).
                        rawTraceId = w3CActivity.TraceId;
                        traceId = Convert.ToUInt64(w3CActivity.TraceId.Substring(16), 16);
                    }
                }
            }

            var finalServiceName = serviceName ?? DefaultServiceName;
            return new SpanContext(parent, traceContext, finalServiceName, traceId: traceId, spanId: spanId, rawTraceId: rawTraceId, rawSpanId: rawSpanId);
        }

        internal Scope StartActiveInternal(string operationName, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, bool finishOnClose = true, ITags tags = null)
        {
            var span = StartSpan(operationName, tags, parent, serviceName, startTime);
            return TracerManager.ScopeManager.Activate(span, finishOnClose);
        }

        internal Span StartSpan(string operationName, ITags tags = null, ISpanContext parent = null, string serviceName = null, DateTimeOffset? startTime = null, ulong? traceId = null, ulong? spanId = null, string rawTraceId = null, string rawSpanId = null, bool addToTraceContext = true)
        {
            var spanContext = CreateSpanContext(parent, serviceName, traceId, spanId, rawTraceId, rawSpanId);

            var span = new Span(spanContext, startTime, tags)
            {
                OperationName = operationName,
            };

            // Apply any global tags
            if (Settings.GlobalTags.Count > 0)
            {
                foreach (var entry in Settings.GlobalTags)
                {
                    span.SetTag(entry.Key, entry.Value);
                }
            }

            // automatically add the "env" tag if defined, taking precedence over an "env" tag set from a global tag
            var env = Settings.Environment;
            if (!string.IsNullOrWhiteSpace(env))
            {
                span.SetTag(Tags.Env, env);
            }

            // automatically add the "version" tag if defined, taking precedence over an "version" tag set from a global tag
            var version = Settings.ServiceVersion;
            if (!string.IsNullOrWhiteSpace(version) && string.Equals(spanContext.ServiceName, DefaultServiceName))
            {
                span.SetTag(Tags.Version, version);
            }

            if (addToTraceContext)
            {
                spanContext.TraceContext.AddSpan(span);
            }

            return span;
        }

        internal Task FlushAsync()
        {
            return TracerManager.AgentWriter.FlushTracesAsync();
        }
    }
}
