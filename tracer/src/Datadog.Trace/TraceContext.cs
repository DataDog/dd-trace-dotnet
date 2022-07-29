// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Globalization;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    internal class TraceContext
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceContext>();

        private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
        private readonly long _timestamp = Stopwatch.GetTimestamp();
        private ArrayBuilder<Span> _spans;
        private bool _rootSpanSent;
        private bool _rootSpanInNextBatch;

        private bool _hasErrorSpans;
        private bool _shouldKeepTrace;
        private int _openSpans;
        private int? _samplingPriority;

        public TraceContext(IDatadogTracer tracer, TraceTagCollection tags = null)
        {
            Tracer = tracer;
            Tags = tags ?? new TraceTagCollection(tracer?.Settings?.OutgoingTagPropagationHeaderMaxLength ?? TagPropagation.OutgoingTagPropagationHeaderMaxLength);
        }

        public Span RootSpan { get; private set; }

        public DateTimeOffset UtcNow => _utcStart.Add(Elapsed);

        public IDatadogTracer Tracer { get; }

        /// <summary>
        /// Gets the collection of trace-level tags.
        /// </summary>
        public TraceTagCollection Tags { get; }

        /// <summary>
        /// Gets the trace's sampling priority.
        /// </summary>
        public int? SamplingPriority
        {
            get => _samplingPriority;
        }

        private TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp);

        public void AddSpan(Span span)
        {
            lock (this)
            {
                if (RootSpan == null)
                {
                    // first span added is the root span
                    RootSpan = span;

                    if (_samplingPriority == null)
                    {
                        if (span.Context.Parent is SpanContext { SamplingPriority: { } samplingPriority })
                        {
                            // this is a local root span created from a propagated context that contains a sampling priority.
                            // any distributed tags were already parsed from SpanContext.PropagatedTags and added to the TraceContext.
                            SetSamplingPriority(samplingPriority);
                        }
                        else
                        {
                            // this is a local root span with no upstream service.
                            // make a sampling decision early so it's ready if we need it for propagation.
                            var samplingDecision = Tracer.Sampler?.MakeSamplingDecision(span) ?? SamplingDecision.Default;
                            SetSamplingPriority(samplingDecision);
                        }
                    }
                }

                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            bool ShouldTriggerPartialFlush() => Tracer.Settings.Exporter.PartialFlushEnabled && _spans.Count >= Tracer.Settings.Exporter.PartialFlushMinSpans;

            ArraySegment<Span> spansToWrite = default;
            var rootSpanInNextBatch = false;
            bool shouldKeepSpan = true;
            bool shouldKeepTraceFinal = default;

            // For now, assume that we have enabled stats computation AND the agent has been confirmed to be compatible
            // We'll need to redo this because we should asynchronously send the agent compatibility check and then set enabled/disabled accordingly
            if (Tracer.CanDropP0s)
            {
                shouldKeepSpan = ShouldKeepSpan(span);
            }

            lock (this)
            {
                _spans.Add(span);
                if (!_rootSpanSent)
                {
                    _rootSpanInNextBatch |= (span == RootSpan);
                    rootSpanInNextBatch = _rootSpanInNextBatch;
                }

                _openSpans--;
                if (shouldKeepSpan)
                {
                    _shouldKeepTrace = true;
                }

                if (_openSpans == 0)
                {
                    spansToWrite = _spans.GetArray();
                    _spans = default;
                }
                else if (ShouldTriggerPartialFlush())
                {
                    Log.Debug<ulong, ulong, int>(
                        "Closing span {spanId} triggered a partial flush of trace {traceId} with {spanCount} pending spans",
                        span.SpanId,
                        span.TraceId,
                        _spans.Count);

                    spansToWrite = _spans.GetArray();

                    // Making the assumption that, if the number of closed spans was big enough to trigger partial flush,
                    // the number of remaining spans is probably big as well.
                    // Therefore, we bypass the resize logic and immediately allocate the array to its maximum size
                    _spans = new ArrayBuilder<Span>(spansToWrite.Count);
                }

                // TODO: I guess we should also report the number of P0 traces and P0 spans were dropped?
                // The Go tracer seems to add this in a trace requst header, that only increments
                // Gotta feed that into the Tracer somehow
                shouldKeepTraceFinal = _shouldKeepTrace;
            }

            if (spansToWrite.Count > 0)
            {
                // When receiving chunks of spans, the backend checks whether the aas.resource.id tag is present on any of the
                // span to decide which metric to emit (datadog.apm.host.instance or datadog.apm.azure_resource_instance one).
                AddAASMetadata(spansToWrite.Array![0]);
                PropagateSamplingPriority(span, spansToWrite, rootSpanInNextBatch);

                Tracer.Write(spansToWrite, shouldKeepTraceFinal);
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

        private static void AddSamplingPriorityTags(Span span, int samplingPriority)
        {
            // set sampling priority tag on the span
            if (span.Tags is CommonTags tags)
            {
                tags.SamplingPriority = samplingPriority;
            }
            else
            {
                span.Tags.SetMetric(Metrics.SamplingPriority, samplingPriority);
            }
        }

        private static void AddAASMetadata(Span span)
        {
            if (AzureAppServices.Metadata.IsRelevant)
            {
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesSiteName, AzureAppServices.Metadata.SiteName);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesSiteKind, AzureAppServices.Metadata.SiteKind);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesSiteType, AzureAppServices.Metadata.SiteType);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesResourceGroup, AzureAppServices.Metadata.ResourceGroup);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesSubscriptionId, AzureAppServices.Metadata.SubscriptionId);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesResourceId, AzureAppServices.Metadata.ResourceId);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesInstanceId, AzureAppServices.Metadata.InstanceId);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesInstanceName, AzureAppServices.Metadata.InstanceName);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesOperatingSystem, AzureAppServices.Metadata.OperatingSystem);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesRuntime, AzureAppServices.Metadata.Runtime);
                span.Tags.SetTag(Datadog.Trace.Tags.AzureAppServicesExtensionVersion, AzureAppServices.Metadata.SiteExtensionVersion);
            }
        }

        // Based on the Go implementation. Cross-check with Java
        //
        // Keep the entire trace when StatsComputation is not enabled
        // When StatsComputation is enabled, perform the following checks (in the given order) when each span is finished. If any span returns true, keep the entire trace.
        // - Is the current sampling priority > 0? Return true.
        // - Have any errors been seen? Return true
        // - If the current span doesn't have Metrics["_dd1.sr.eausr"] (aka AnalyticsSampleRate), skip. If it does, run the Knuth sampling decision on the metric value and return the result.
        private bool ShouldKeepSpan(Span span)
        {
            if (_samplingPriority is int a && a > 0)
            {
                return true;
            }

            // TODO: Is there a better way to read and write this value? Open to suggestions
            lock (this)
            {
                if (_hasErrorSpans)
                {
                    return true;
                }
                else if (span.Error)
                {
                    _hasErrorSpans = true;
                    return true;
                }
            }

            if (span.GetMetric(Trace.Tags.Analytics) is double rate)
            {
                return ((span.TraceId * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);
            }

            return false;
        }

        private void PropagateSamplingPriority(Span closedSpan, ArraySegment<Span> spansToWrite, bool containsRootSpan)
        {
            if (_samplingPriority == null)
            {
                return;
            }

            // This should be the most common case as usually we close the root span last
            if (closedSpan == RootSpan)
            {
                AddSamplingPriorityTags(closedSpan, _samplingPriority.Value);
                _rootSpanSent = true;
                return;
            }

            // Normally this use case never happens as the last span closed is the rootspan usually.
            // So we should have fallen in the previous case.
            // we check for _rootSpanSent as well as there's a slight possibility it is true as we set it out of the lock
            if (!_rootSpanSent && containsRootSpan)
            {
                // Using a for loop to avoid the boxing allocation on ArraySegment.GetEnumerator
                for (var i = 0; i < spansToWrite.Count; i++)
                {
                    var span = spansToWrite.Array![i + spansToWrite.Offset];
                    if (span == RootSpan)
                    {
                        AddSamplingPriorityTags(span, _samplingPriority.Value);
                        _rootSpanSent = true;
                        return;
                    }
                }

                Log.Warning("Root span wasn't found even though expected here.");
            }

            // Here we must be in the case when rootspan has already been sent or we are in a partial flush.
            // Agent versions < 7.34.0 look for the sampling priority in one of the spans whose parent is not found in the same chunk.
            // If there are multiple orphans, the agent picks one nondeterministically and does not check the others.
            // Finding those spans is not trivial, so instead we apply the priority to every span.
            for (var i = 0; i < spansToWrite.Count; i++)
            {
                AddSamplingPriorityTags(spansToWrite.Array![i + spansToWrite.Offset], _samplingPriority.Value);
            }
        }
    }
}
