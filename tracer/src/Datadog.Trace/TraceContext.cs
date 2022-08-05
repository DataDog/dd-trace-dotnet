// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    internal class TraceContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceContext>();

        private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
        private readonly long _timestamp = Stopwatch.GetTimestamp();
        private bool _rootSpanSent = false;
        private bool _rootSpanInNextBatch = false;
        private ArrayBuilder<Span> _spans;

        private int _openSpans;
        private int? _samplingPriority;

        public TraceContext(IDatadogTracer tracer, TraceTagCollection tags = null)
        {
            Tracer = tracer;
            Tags = tags ?? new TraceTagCollection();
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
                        if (span.Context.Parent is SpanContext context && context.SamplingPriority != null)
                        {
                            // this is a root span created from a propagated context that contains a sampling priority.
                            // lock sampling priority when a span is started from a propagated trace.
                            _samplingPriority = context.SamplingPriority;
                        }
                        else
                        {
                            // this is a local root span (i.e. not propagated).
                            // determine an initial sampling priority for this trace, but don't lock it yet
                            _samplingPriority = Tracer.Sampler?.GetSamplingPriority(RootSpan);
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

            lock (this)
            {
                _spans.Add(span);
                if (!_rootSpanSent)
                {
                    _rootSpanInNextBatch |= (span == RootSpan);
                    rootSpanInNextBatch = _rootSpanInNextBatch;
                }

                _openSpans--;

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
            }

            if (spansToWrite.Count > 0)
            {
                // When receiving chunks of spans, the backend checks whether the aas.resource.id tag is present on any of the
                // span to decide which metric to emit (datadog.apm.host.instance or datadog.apm.azure_resource_instance one).
                AddAASMetadata(spansToWrite.Array[0]);
                PropagateSamplingPriority(span, spansToWrite, rootSpanInNextBatch);

                Tracer.Write(spansToWrite);
            }
        }

        public void SetSamplingPriority(int? samplingPriority, bool notifyDistributedTracer = true)
        {
            _samplingPriority = samplingPriority;

            if (notifyDistributedTracer)
            {
                DistributedTracer.Instance.SetSamplingPriority(samplingPriority);
            }
        }

        public TimeSpan ElapsedSince(DateTimeOffset date)
        {
            return Elapsed + (_utcStart - date);
        }

        private static void AddSamplingPriorityTags(Span span, int samplingPriority)
        {
            if (span.Tags is CommonTags tags)
            {
                tags.SamplingPriority = samplingPriority;
            }
            else
            {
                span.Tags.SetMetric(Metrics.SamplingPriority, samplingPriority);
            }
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
            if (containsRootSpan)
            {
                // Using a for loop to avoid the boxing allocation on ArraySegment.GetEnumerator
                for (var i = 0; i < spansToWrite.Count; i++)
                {
                    var span = spansToWrite.Array[i + spansToWrite.Offset];
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
                AddSamplingPriorityTags(spansToWrite.Array[i + spansToWrite.Offset], _samplingPriority.Value);
            }
        }

        private void AddAASMetadata(Span span)
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
    }
}
