// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    internal class TraceContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceContext>();

        private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
        private readonly long _timestamp = Stopwatch.GetTimestamp();
        private readonly object _syncRoot = new();
        private IastRequestContext _iastRequestContext;

        private ArrayBuilder<Span> _spans;
        private int _openSpans;
        private int? _samplingPriority;

        public TraceContext(IDatadogTracer tracer, TraceTagCollection tags = null)
        {
            var settings = tracer?.Settings;

            // TODO: Environment, ServiceVersion, GitCommitSha, and GitRepositoryUrl are stored on the TraceContext
            // even though they likely won't change for the lifetime of the process. We should consider moving them
            // elsewhere to reduce the memory usage.
            if (settings is not null)
            {
                // these could be set from DD_ENV/DD_VERSION or from DD_TAGS
                Environment = settings.Environment;
                ServiceVersion = settings.ServiceVersion;
            }

            Tracer = tracer;
            Tags = tags ?? new TraceTagCollection();
        }

        public Span RootSpan { get; private set; }

        public DateTimeOffset UtcNow => _utcStart.Add(Elapsed);

        public IDatadogTracer Tracer { get; }

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

        private TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp);

        internal void EnableIastInRequest()
        {
            if (_iastRequestContext is null)
            {
                lock (_syncRoot)
                {
                    _iastRequestContext ??= new();
                }
            }
        }

        public void AddSpan(Span span)
        {
            lock (_syncRoot)
            {
                if (RootSpan == null)
                {
                    // first span added is the local root span
                    RootSpan = span;

                    // if we don't have a sampling priority yet, make a sampling decision now
                    if (_samplingPriority == null)
                    {
                        var samplingDecision = Tracer.Sampler?.MakeSamplingDecision(span) ?? SamplingDecision.Default;
                        SetSamplingPriority(samplingDecision);
                    }
                }

                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            bool ShouldTriggerPartialFlush() => Tracer.Settings.Exporter.PartialFlushEnabled && _spans.Count >= Tracer.Settings.Exporter.PartialFlushMinSpans;

            ArraySegment<Span> spansToWrite = default;

            // Propagate the resource name to the profiler for root web spans
            if (span.IsRootSpan && span.Type == SpanTypes.Web)
            {
                Profiler.Instance.ContextTracker.SetEndpoint(span.RootSpanId, span.ResourceName);

                if (Iast.Iast.Instance.Settings.Enabled && _iastRequestContext != null)
                {
                    _iastRequestContext.AddIastVulnerabilitiesToSpan(span);
                    OverheadController.Instance.ReleaseRequest();
                }
            }

            lock (_syncRoot)
            {
                _spans.Add(span);
                _openSpans--;

                if (_openSpans == 0)
                {
                    spansToWrite = _spans.GetArray();
                    _spans = default;
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
                }
            }

            if (spansToWrite.Count > 0)
            {
                Tracer.Write(spansToWrite);
            }
        }

        // called from tests to force partial flush
        internal void WriteClosedSpans()
        {
            ArraySegment<Span> spansToWrite;

            lock (_syncRoot)
            {
                spansToWrite = _spans.GetArray();
                _spans = default;
            }

            if (spansToWrite.Count > 0)
            {
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
                // set the sampling mechanism trace tag
                // * only set tag if priority is AUTO_KEEP (1) or USER_KEEP (2)
                // * do not overwrite an existing value
                // * don't set tag if sampling mechanism is unknown (null)
                // * the "-" prefix is a left-over separator from a previous iteration of this feature (not a typo or a negative sign)
                var tagValue = $"-{mechanism.Value.ToString(CultureInfo.InvariantCulture)}";
                Tags.TryAddTag(tagName, tagValue);
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

        public TimeSpan ElapsedSince(DateTimeOffset date)
        {
            return Elapsed + (_utcStart - date);
        }
    }
}
