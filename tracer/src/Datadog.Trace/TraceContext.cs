// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    internal sealed class TraceContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceContext>();

        private SpanCollection _spans;
        private int _openSpans;

        private bool _segmentClosed;

        private IastRequestContext? _iastRequestContext;
        private AppSecRequestContext? _appSecRequestContext;

        // Lazily created on the first feature-flag evaluation for this trace; null until then, so
        // traces that never evaluate a flag pay nothing. State dies with the TraceContext.
        private SpanEnrichmentState? _featureFlagEnrichment;

        // _rootSpan was chosen in #4125 to be the lock that protects
        // * _spans
        // * _openSpans
        // * _segmentClosed
        // although it's a nullable field, the _rootSpan must always be set before operations on
        // _spans take place, so it's okay to use it as a lock key
        // even though we need to override the nullable warnings in some places.
        // The reason _rootSpan was chosen is to avoid
        // allocating a separate object for the lock.
        private Span? _rootSpan;

        public TraceContext(IDatadogTracer tracer, TraceTagCollection? tags = null)
        {
            CurrentTraceSettings = tracer.PerTraceSettings;

            // TODO: Environment and ServiceVersion are stored on the TraceContext
            // even though they likely won't change for the lifetime of the process. We should consider moving them
            // elsewhere to reduce the memory usage.
            var settings = CurrentTraceSettings.Settings;
            // these could be set from DD_ENV/DD_VERSION or from DD_TAGS
            Environment = settings.Environment;
            ServiceVersion = settings.ServiceVersion;

            Tracer = tracer;
            Tags = tags ?? new TraceTagCollection();
            Clock = TraceClock.Instance;
        }

        public Span? RootSpan
        {
            get => _rootSpan;
        }

        public TraceClock Clock { get; }

        public IDatadogTracer Tracer { get; }

        public PerTraceSettings CurrentTraceSettings { get; }

        /// <summary>
        /// Gets the collection of trace-level tags.
        /// </summary>
        public TraceTagCollection Tags { get; }

        /// <summary>
        /// Gets the trace's sampling priority.
        /// </summary>
        public int? SamplingPriority { get; private set; }

        public string? Environment { get; set; }

        public string? ServiceVersion { get; set; }

        public string? Origin { get; set; }

        public string? SamplingMechanism { get; set; }

        public float? AppliedSamplingRate { get; set; }

        public float? RateLimiterRate { get; set; }

        public double? TracesKeepRate { get; set; }

        /// <summary>
        /// Gets or sets additional key/value pairs from upstream "tracestate" header that we will propagate downstream.
        /// This value will _not_ include the "dd" key, which is parsed out into other individual values
        /// (e.g. sampling priority, origin, propagates tags, etc).
        /// </summary>
        internal string? AdditionalW3CTraceState { get; set; }

        /// <summary>
        /// Gets or sets the raw content of the inbound/rewritten W3C tracestate "ot=" member
        /// (OpenTelemetry consistent-probability-sampling sub-keys), with no "ot=" prefix.
        /// Null means there is nothing to emit. Never decoded into typed fields — see
        /// <see cref="Propagators.OtelTraceStateHelpers"/> for the only code that inspects
        /// or rewrites its "rv"/"th" sub-keys.
        /// </summary>
        internal string? OtelTraceState { get; set; }

        /// <summary> Gets the IAST context </summary>
        internal IastRequestContext? IastRequestContext => _iastRequestContext;

        /// <summary> Gets the AppSec context </summary>
        internal AppSecRequestContext AppSecRequestContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _appSecRequestContext) ?? CreateAppSecRequestContext();
        }

        internal bool WafExecuted { get; set; }

        /// <summary> Gets the feature-flag span-enrichment state for this trace, or null if no flag has been evaluated. </summary>
        internal SpanEnrichmentState? FeatureFlagEnrichment => Volatile.Read(ref _featureFlagEnrichment);

        internal static TraceContext? GetTraceContext(in SpanCollection spans)
            => spans.FirstSpan?.Context.TraceContext;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private AppSecRequestContext CreateAppSecRequestContext()
        {
            if (_rootSpan is not { } rootSpan)
            {
                var created = new AppSecRequestContext();
                return Interlocked.CompareExchange(ref _appSecRequestContext, created, null) ?? created;
            }

            lock (rootSpan)
            {
                var created = _segmentClosed || (rootSpan.Type == SpanTypes.Web && rootSpan.IsFinished)
                                  ? AppSecRequestContext.CreateWithDisposedAdditiveContext()
                                  : new AppSecRequestContext();

                return Interlocked.CompareExchange(ref _appSecRequestContext, created, null) ?? created;
            }
        }

        /// <summary>
        /// Gets the feature-flag span-enrichment state for this trace (created on first use), or null
        /// when span enrichment is disabled.
        /// </summary>
        internal SpanEnrichmentState? GetOrCreateFeatureFlagEnrichment()
        {
            if (!Tracer.Settings.IsSpanEnrichmentEnabled)
            {
                return null;
            }

            if (Volatile.Read(ref _featureFlagEnrichment) is null)
            {
                Interlocked.CompareExchange(ref _featureFlagEnrichment, new(), null);
            }

            return _featureFlagEnrichment;
        }

        internal void EnableIastInRequest()
        {
            if (Volatile.Read(ref _iastRequestContext) is null)
            {
                Interlocked.CompareExchange(ref _iastRequestContext, new(), null);
            }
        }

        public void AddSpan(Span span)
        {
            // first span added is the local root span
            if (Interlocked.CompareExchange(ref _rootSpan, span, null) == null)
            {
                span.MarkSpanForExceptionReplay();
            }

            lock (_rootSpan)
            {
                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            bool ShouldTriggerPartialFlush() => Tracer.Settings.PartialFlushEnabled && _spans.Count >= Tracer.Settings.PartialFlushMinSpans;

            SpanCollection spansToWrite = default;

            // Propagate the resource name to the profiler for root web spans
            if (span.IsRootSpan)
            {
                if (span.Type == SpanTypes.Web)
                {
                    Profiler.Instance.ContextTracker.SetEndpoint(span.RootSpanId, span.ResourceName);

                    var iastInstance = Iast.Iast.Instance;
                    if (iastInstance.Settings.Enabled)
                    {
                        if (_iastRequestContext is { } iastRequestContext)
                        {
                            iastRequestContext.AddIastVulnerabilitiesToSpan(span);
                            iastInstance.OverheadController.ReleaseRequest();
                        }
                        else
                        {
                            IastRequestContext.AddIastDisabledFlagToSpan(span);
                        }
                    }

                    Volatile.Read(ref _appSecRequestContext)?.CloseWebSpan(span);
                }
            }

            if (span.ServiceName is not null &&
                !string.Equals(span.ServiceName, Tracer.DefaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                ExtraServicesProvider.Instance.AddService(span.ServiceName);
            }

            var disposeAdditiveContext = span.IsRootSpan && span.Type == SpanTypes.Web;

            lock (_rootSpan!)
            {
                _spans = SpanCollection.Append(in _spans, span);
                _openSpans--;

                if (_openSpans == 0)
                {
                    spansToWrite = _spans;
                    _spans = default;
                    _segmentClosed = true;
                    disposeAdditiveContext = true;
                    TelemetryFactory.Metrics.RecordCountTraceSegmentsClosed();
                }
                else if (TestOptimization.Instance.IsRunning && span.IsCiVisibilitySpan())
                {
                    // TestSession, TestModule, TestSuite, Test and Browser spans are part of CI Visibility
                    // all of them are known to be Root spans, so we can flush them as soon as they are closed
                    // even if their children have not been closed yet.
                    // An unclosed/unfinished child span should never block the report of a test.
                    spansToWrite = _spans;
                    _spans = default;
                    TelemetryFactory.Metrics.RecordCountTraceSegmentsClosed();
                }
                else if (ShouldTriggerPartialFlush())
                {
                    Log.Debug<ulong, string, int>(
                        "Closing span {SpanId} triggered a partial flush of trace {TraceId} with {SpanCount} pending spans",
                        span.SpanId,
                        span.Context.RawTraceId,
                        _spans.Count);

                    spansToWrite = _spans;

                    // Making the assumption that, if the number of closed spans was big enough to trigger partial flush,
                    // the number of remaining spans is probably big as well.
                    // Therefore, we bypass the resize logic and immediately allocate the array to its maximum size
                    _spans = new SpanCollection(spansToWrite.Count);
                    TelemetryFactory.Metrics.RecordCountTracePartialFlush(MetricTags.PartialFlushReason.LargeTrace);
                }

                if (disposeAdditiveContext)
                {
                    _appSecRequestContext?.DisposeAdditiveContext();
                }
            }

            if (spansToWrite.Count > 0)
            {
                GetOrMakeSamplingDecision();
                RunSpanSampler(in spansToWrite);
                Tracer.Write(in spansToWrite);
            }
        }

        [TestingOnly]
        internal void WriteClosedSpans()
        {
            SpanCollection spansToWrite;

            lock (_rootSpan!)
            {
                spansToWrite = _spans;
                _spans = default;
            }

            if (spansToWrite.Count > 0)
            {
                GetOrMakeSamplingDecision();
                RunSpanSampler(in spansToWrite);
                Tracer.Write(in spansToWrite);
            }
        }

        public int GetOrMakeSamplingDecision()
        {
            if (SamplingPriority is { } samplingPriority)
            {
                // common case: we already have a sampling decision
                return samplingPriority;
            }

            return GetOrMakeSamplingDecision(_rootSpan);
        }

        public int GetOrMakeSamplingDecision(Span? span)
        {
            if (span is null)
            {
                // we can't make a sampling decision without a root span because:
                // - we need a trace id, and for now trace id lives in SpanContext, not in TraceContext
                // - we need to apply sampling rules to the root span

                // note we do not set SamplingDecision
                // so it remains null and we can try again later
                return SamplingPriorityValues.Default;
            }

            var samplingDecision = CurrentTraceSettings?.TraceSampler is { } sampler
                                       ? sampler.MakeSamplingDecision(span)
                                       : SamplingDecision.Default;

            SetSamplingPriority(
                samplingDecision.Priority,
                samplingDecision.Mechanism,
                samplingDecision.Rate,
                samplingDecision.LimiterRate,
                sample: samplingDecision.Sample);

            return samplingDecision.Priority;
        }

        public void SetSamplingPriority(
            int? priority,
            string? mechanism = null,
            float? rate = null,
            float? limiterRate = null,
            bool notifyDistributedTracer = true,
            bool? sample = null)
        {
            if (priority is not { } p)
            {
                return;
            }

            var isLocalRoot = SamplingPriority is null;

            // priority (keep/drop) can change (manually, ASM, etc)
            SamplingPriority = priority;

            // report only the original rates, do not override
            AppliedSamplingRate ??= rate;
            RateLimiterRate ??= limiterRate;
            SamplingMechanism ??= mechanism;

            if (SamplingPriorityValues.IsKeep(p) && mechanism != null)
            {
                // report sampling mechanism as trace tag only if decision is to keep the trace.
                // report only the original sampling mechanism, do not override.
                Tags.TryAddTag(Trace.Tags.Propagated.DecisionMaker, mechanism);
            }
            else if (SamplingPriorityValues.IsDrop(p))
            {
                // remove sampling mechanism trace tag if decision is to drop the trace.
                // do not set SamplingMechanism = null because that would allow changing the mechanism later,
                // which is not allowed.
                Tags.RemoveTag(Trace.Tags.Propagated.DecisionMaker);
            }

            if (rate is { } samplingRate && samplingRate is >= 0f and <= 1f)
            {
                // set Knuth sampling rate as a propagated tag for agent and rule-based sampling only:
                // "Default" means no agent-configured rate has been received yet (client-side fallback),
                // and must not propagate as _dd.p.ksr, to stay consistent with other tracers.
                if (mechanism is Sampling.SamplingMechanism.AgentRate
                              or Sampling.SamplingMechanism.LocalTraceSamplingRule
                              or Sampling.SamplingMechanism.RemoteAdaptiveSamplingRule
                              or Sampling.SamplingMechanism.RemoteUserSamplingRule)
                {
                    // format with up to 6 decimal digits, no trailing zeros (per RFC)
                    Tags.TryAddTag(Trace.Tags.Propagated.KnuthSamplingRate, samplingRate.ToString("0.######", CultureInfo.InvariantCulture));
                }

                // (for OTel interop) derive/erase the "ot=" tracestate rv/th sub-keys for W3C injection on every root
                // probability decision, including the "Default" mechanism fallback rate.
                if (isLocalRoot && IsW3CTraceContextInjectionEnabled() && sample is { } didSample && RootSpan is { } rootSpan
                                && mechanism is Sampling.SamplingMechanism.AgentRate
                                             or Sampling.SamplingMechanism.LocalTraceSamplingRule
                                             or Sampling.SamplingMechanism.RemoteAdaptiveSamplingRule
                                             or Sampling.SamplingMechanism.RemoteUserSamplingRule
                                             or Sampling.SamplingMechanism.Default)
                {
                    var h = SamplingHelpers.ComputeKnuthHash(rootSpan.TraceId128.Lower);
                    var rv = (~h) >> 8;
                    // round-trip the rate through decimal first: samplingRate is a float, and widening it to
                    // double directly keeps its 32-bit mantissa noise in the low bits of the 56-bit threshold.
                    var th = (ulong)Math.Round((1.0 - (double)(decimal)samplingRate) * (1UL << 56), MidpointRounding.AwayFromZero);

                    // clamp th into the valid 56-bit domain: rate=0.0f rounds up to 2^56, one bit out of range.
                    th = Math.Min(th, (1UL << 56) - 1);

                    // 64<>56-bit imprecision clamp (design doc Decision 2 / RFC §7):
                    // force agreement between the (rv, th) pair and DD's actual keep/drop decision.
                    if (didSample && rv < th)
                    {
                        rv = th;
                    }
                    else if (!didSample && rv >= th)
                    {
                        rv = th > 0 ? th - 1 : 0;
                    }

                    // Rate-limiter demotion removes th in the same rewrite.
                    OtelTraceState = OtelTraceStateHelpers.SetRvTh(OtelTraceState, rv, didSample && SamplingPriorityValues.IsDrop(p) ? null : th);
                }
            }
            else if (mechanism is Sampling.SamplingMechanism.Manual or Sampling.SamplingMechanism.Asm)
            {
                OtelTraceState = OtelTraceStateHelpers.SetRvTh(OtelTraceState, OtelTraceStateHelpers.ExtractRv(OtelTraceState), th: null);
            }

            if (notifyDistributedTracer)
            {
                DistributedTracer.Instance.SetSamplingPriority(priority);
            }
        }

        private bool IsW3CTraceContextInjectionEnabled()
        {
            foreach (var style in Tracer.Settings.PropagationStyleInject)
            {
                if (string.Equals(style, ContextPropagationHeaderStyle.W3CTraceContext, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(style, ContextPropagationHeaderStyle.Deprecated.W3CTraceContext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void RunSpanSampler(in SpanCollection spans)
        {
            if (CurrentTraceSettings?.SpanSampler is null)
            {
                return;
            }

            if (SamplingPriority is { } samplingPriority && SamplingPriorityValues.IsDrop(samplingPriority))
            {
                foreach (var span in spans)
                {
                    CurrentTraceSettings.SpanSampler.MakeSamplingDecision(span);
                }
            }
        }
    }
}
