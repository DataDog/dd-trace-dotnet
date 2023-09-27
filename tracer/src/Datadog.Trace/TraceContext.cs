// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Datadog.Trace.AppSec;
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

        private readonly TraceClock _clock;

        private IastRequestContext _iastRequestContext;

        private ArrayBuilder<Span> _spans;
        private int _openSpans;
        private int? _samplingPriority;
        private Span _rootSpan;

        public TraceContext(IDatadogTracer tracer, TraceTagCollection tags = null)
        {
            CurrentTraceSettings = tracer.PerTraceSettings;

            var settings = tracer.Settings;

            // TODO: Environment, ServiceVersion, GitCommitSha, and GitRepositoryUrl are stored on the TraceContext
            // even though they likely won't change for the lifetime of the process. We should consider moving them
            // elsewhere to reduce the memory usage.
            if (settings is not null)
            {
                // these could be set from DD_ENV/DD_VERSION or from DD_TAGS
                Environment = settings.EnvironmentInternal;
                ServiceVersion = settings.ServiceVersionInternal;
            }

            Tracer = tracer;
            Tags = tags ?? new TraceTagCollection();
            _clock = TraceClock.Instance;
        }

        public Span RootSpan
        {
            get => _rootSpan;
            private set => _rootSpan = value;
        }

        public TraceClock Clock => _clock;

        public IDatadogTracer Tracer { get; }

        public PerTraceSettings CurrentTraceSettings { get; }

        /// <summary>
        /// Gets the collection of trace-level tags.
        /// </summary>
        [NotNull]
        public TraceTagCollection Tags { get; }

        /// <summary>
        /// Gets the trace's sampling priority.
        /// </summary>
        public int? SamplingPriority
        {
            get => _samplingPriority;
        }

        public string Environment { get; set; }

        public string ServiceVersion { get; set; }

        public string Origin { get; set; }

        /// <summary>
        /// Gets or sets additional key/value pairs from upstream "tracestate" header that we will propagate downstream.
        /// This value will _not_ include the "dd" key, which is parsed out into other individual values
        /// (e.g. sampling priority, origin, propagates tags, etc).
        /// </summary>
        internal string AdditionalW3CTraceState { get; set; }

        /// <summary>
        /// Gets the IAST context.
        /// </summary>
        internal IastRequestContext IastRequestContext => _iastRequestContext;

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
                // if we don't have a sampling priority yet, make a sampling decision now
                if (_samplingPriority == null)
                {
                    SetSamplingPriority(CurrentTraceSettings?.TraceSampler?.MakeSamplingDecision(span) ?? SamplingDecision.Default);
                }
            }

            lock (_rootSpan)
            {
                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            bool ShouldTriggerPartialFlush() => Tracer.Settings.ExporterInternal.PartialFlushEnabledInternal && _spans.Count >= Tracer.Settings.ExporterInternal.PartialFlushMinSpansInternal;

            ArraySegment<Span> spansToWrite = default;

            // Propagate the resource name to the profiler for root web spans
            if (span.IsRootSpan && span.Type == SpanTypes.Web)
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

                Security.Instance.ApiSecurity.ReleaseRequest();
            }

            if (!string.Equals(span.ServiceName, Tracer.DefaultServiceName, StringComparison.OrdinalIgnoreCase))
            {
                ExtraServicesProvider.Instance.AddService(span.ServiceName);
            }

            lock (_rootSpan)
            {
                _spans.Add(span);
                _openSpans--;

                if (_openSpans == 0)
                {
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
                RunSpanSampler(spansToWrite);
                Tracer.Write(spansToWrite);
            }
        }

        // called from tests to force partial flush
        internal void WriteClosedSpans()
        {
            ArraySegment<Span> spansToWrite;

            lock (_rootSpan)
            {
                spansToWrite = _spans.GetArray();
                _spans = default;
            }

            if (spansToWrite.Count > 0)
            {
                RunSpanSampler(spansToWrite);
                Tracer.Write(spansToWrite);
            }
        }

        public void SetSamplingPriority(SamplingDecision decision, bool notifyDistributedTracer = true)
        {
            SetSamplingPriority(decision.Priority, decision.Mechanism, notifyDistributedTracer);
        }

        public void SetSamplingPriority(int? priority, int? mechanism = null, bool notifyDistributedTracer = true)
        {
            if (priority == null)
            {
                return;
            }

            _samplingPriority = priority;

            const string tagName = Trace.Tags.Propagated.DecisionMaker;

            if (priority > 0 && mechanism != null)
            {
                Tags.TryAddTag(tagName, SamplingMechanism.GetTagValue(mechanism.Value));
            }
            else if (priority <= 0)
            {
                // remove tag if priority is AUTO_DROP (0) or USER_DROP (-1)
                Tags.RemoveTag(tagName);
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

            if (spans.Array![spans.Offset].Context.TraceContext?.SamplingPriority <= 0)
            {
                for (int i = 0; i < spans.Count; i++)
                {
                    CurrentTraceSettings.SpanSampler.MakeSamplingDecision(spans.Array[i + spans.Offset]);
                }
            }
        }
    }
}
