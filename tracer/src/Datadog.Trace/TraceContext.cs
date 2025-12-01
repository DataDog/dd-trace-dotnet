// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    internal class TraceContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceContext>();

        private ArrayBuilder<Span> _spans;
        private int _openSpans;

        private IastRequestContext? _iastRequestContext;
        private AppSecRequestContext? _appSecRequestContext;

        // _rootSpan was chosen in #4125 to be the lock that protects
        // * _spans
        // * _openSpans
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

        /// <summary> Gets the IAST context </summary>
        internal IastRequestContext? IastRequestContext => _iastRequestContext;

        /// <summary> Gets the AppSec context </summary>
        internal AppSecRequestContext AppSecRequestContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Volatile.Read(ref _appSecRequestContext) is null)
                {
                    Interlocked.CompareExchange(ref _appSecRequestContext, new(), null);
                }

                return _appSecRequestContext!;
            }
        }

        internal bool WafExecuted { get; set; }

        internal static TraceContext? GetTraceContext(in ArraySegment<Span> spans) =>
            spans.Count > 0 ? spans.Array![spans.Offset].Context.TraceContext : null;

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

            ArraySegment<Span> spansToWrite = default;

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

                    if (_appSecRequestContext is not null)
                    {
                        _appSecRequestContext.CloseWebSpan(span);
                        _appSecRequestContext.DisposeAdditiveContext();
                    }
                }
            }

            if (span.ServiceName is not null &&
                !string.Equals(span.ServiceName, Tracer.DefaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                ExtraServicesProvider.Instance.AddService(span.ServiceName);
            }

            lock (_rootSpan!)
            {
                _spans.Add(span);
                _openSpans--;

                if (_openSpans == 0)
                {
                    spansToWrite = _spans.GetArray();
                    _spans = default;
                    TelemetryFactory.Metrics.RecordCountTraceSegmentsClosed();
                }
                else if (TestOptimization.Instance.IsRunning && span.IsCiVisibilitySpan())
                {
                    // TestSession, TestModule, TestSuite, Test and Browser spans are part of CI Visibility
                    // all of them are known to be Root spans, so we can flush them as soon as they are closed
                    // even if their children have not been closed yet.
                    // An unclosed/unfinished child span should never block the report of a test.
                    spansToWrite = _spans.GetArray();
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

                    spansToWrite = _spans.GetArray();

                    // Making the assumption that, if the number of closed spans was big enough to trigger partial flush,
                    // the number of remaining spans is probably big as well.
                    // Therefore, we bypass the resize logic and immediately allocate the array to its maximum size
                    _spans = new ArrayBuilder<Span>(spansToWrite.Count);
                    TelemetryFactory.Metrics.RecordCountTracePartialFlush(MetricTags.PartialFlushReason.LargeTrace);
                }
            }

            if (spansToWrite.Count > 0)
            {
                GetOrMakeSamplingDecision();
                RunSpanSampler(spansToWrite);
                Tracer.Write(spansToWrite);
            }
        }

        // called from tests to force partial flush
        internal void WriteClosedSpans()
        {
            ArraySegment<Span> spansToWrite;

            lock (_rootSpan!)
            {
                spansToWrite = _spans.GetArray();
                _spans = default;
            }

            if (spansToWrite.Count > 0)
            {
                GetOrMakeSamplingDecision();
                RunSpanSampler(spansToWrite);
                Tracer.Write(spansToWrite);
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
                samplingDecision.LimiterRate);

            return samplingDecision.Priority;
        }

        public void SetSamplingPriority(
            int? priority,
            string? mechanism = null,
            float? rate = null,
            float? limiterRate = null,
            bool notifyDistributedTracer = true)
        {
            if (priority is not { } p)
            {
                return;
            }

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

                // Set Knuth sampling rate (_dd.p.ksr) for applicable mechanisms
                // This tag is used for backend resampling to compute effective sampling rates
                if (rate.HasValue &&
                    mechanism is SamplingMechanism.AgentRate
                                or SamplingMechanism.LocalTraceSamplingRule
                                or SamplingMechanism.RemoteUserSamplingRule
                                or SamplingMechanism.RemoteAdaptiveSamplingRule)
                {
                    // Format with up to 6 significant digits, no trailing zeros (e.g., "0.5" not "0.500000")
                    Tags.TryAddTag(Trace.Tags.Propagated.KnuthSamplingRate, rate.Value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            else if (SamplingPriorityValues.IsDrop(p))
            {
                // remove sampling mechanism trace tag if decision is to drop the trace.
                // do not set SamplingMechanism = null because that would allow changing the mechanism later,
                // which is not allowed.
                Tags.RemoveTag(Trace.Tags.Propagated.DecisionMaker);
                Tags.RemoveTag(Trace.Tags.Propagated.KnuthSamplingRate);
            }

            if (notifyDistributedTracer)
            {
                DistributedTracer.Instance.SetSamplingPriority(priority);
            }
        }

        private void RunSpanSampler(ArraySegment<Span> spans)
        {
            if (CurrentTraceSettings?.SpanSampler is null)
            {
                return;
            }

            if (SamplingPriority is { } samplingPriority && SamplingPriorityValues.IsDrop(samplingPriority))
            {
                for (int i = 0; i < spans.Count; i++)
                {
                    var span = spans.Array![i + spans.Offset];
                    CurrentTraceSettings.SpanSampler.MakeSamplingDecision(span);
                }
            }
        }
    }
}
