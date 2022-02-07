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

        private readonly object _globalLock = new();
        private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
        private readonly long _timestamp = Stopwatch.GetTimestamp();
        private ArrayBuilder<Span> _spans;

        private int _openSpans;
        private int? _samplingPriority;

        public TraceContext(IDatadogTracer tracer)
        {
            Tracer = tracer;
        }

        public Span RootSpan { get; private set; }

        public DateTimeOffset UtcNow => _utcStart.Add(Elapsed);

        public IDatadogTracer Tracer { get; }

        /// <summary>
        /// Gets the trace's sampling priority.
        /// </summary>
        public int? SamplingPriority
        {
            get => _samplingPriority;
        }

        private TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp);

        /// <summary>
        /// Gets or sets a collection of propagated internal Datadog tags,
        /// formatted as "key1=value1,key2=value2".
        /// </summary>
        /// <remarks>
        /// We're keeping this as the string representation to avoid having to parse.
        /// For now, it's relatively easy to append new values when needed.
        /// </remarks>
        public string DatadogTags { get; set; }

        public void AddSpan(Span span)
        {
            lock (_globalLock)
            {
                if (RootSpan == null)
                {
                    // first span added is the root span
                    RootSpan = span;
                    DecorateRootSpan(span);

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

            if (span == RootSpan)
            {
                if (_samplingPriority == null)
                {
                    Log.Warning("Cannot set span metric for sampling priority before it has been set.");
                }
                else
                {
                    AddSamplingPriorityTags(span, _samplingPriority.Value);
                }
            }

            ArraySegment<Span> spansToWrite = default;

            bool shouldPropagateMetadata = false;

            lock (_globalLock)
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
                    Log.Debug<ulong, ulong, int>(
                        "Closing span {spanId} triggered a partial flush of trace {traceId} with {spanCount} pending spans",
                        span.SpanId,
                        span.TraceId,
                        _spans.Count);

                    // We may not be sending the root span, so we need to propagate the metadata to other spans of the partial trace
                    // There's no point in doing that inside of the lock, so we set a flag for later
                    shouldPropagateMetadata = true;

                    spansToWrite = _spans.GetArray();

                    // Making the assumption that, if the number of closed spans was big enough to trigger partial flush,
                    // the number of remaining spans is probably big as well.
                    // Therefore, we bypass the resize logic and immediately allocate the array to its maximum size
                    _spans = new ArrayBuilder<Span>(spansToWrite.Count);
                }
            }

            if (shouldPropagateMetadata)
            {
                PropagateMetadata(spansToWrite);
            }

            if (spansToWrite.Count > 0)
            {
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

        private void PropagateMetadata(ArraySegment<Span> spans)
        {
            // The agent looks for the sampling priority on the first span that has no parent
            // Finding those spans is not trivial, so instead we apply the priority to every span

            var samplingPriority = _samplingPriority;

            if (samplingPriority == null)
            {
                return;
            }

            // Using a for loop to avoid the boxing allocation on ArraySegment.GetEnumerator
            for (int i = 0; i < spans.Count; i++)
            {
                AddSamplingPriorityTags(spans.Array[i + spans.Offset], samplingPriority.Value);
            }
        }

        private void DecorateRootSpan(Span span)
        {
            if (AzureAppServices.Metadata.IsRelevant)
            {
                span.SetTag(Tags.AzureAppServicesSiteName, AzureAppServices.Metadata.SiteName);
                span.SetTag(Tags.AzureAppServicesSiteKind, AzureAppServices.Metadata.SiteKind);
                span.SetTag(Tags.AzureAppServicesSiteType, AzureAppServices.Metadata.SiteType);
                span.SetTag(Tags.AzureAppServicesResourceGroup, AzureAppServices.Metadata.ResourceGroup);
                span.SetTag(Tags.AzureAppServicesSubscriptionId, AzureAppServices.Metadata.SubscriptionId);
                span.SetTag(Tags.AzureAppServicesResourceId, AzureAppServices.Metadata.ResourceId);
                span.SetTag(Tags.AzureAppServicesInstanceId, AzureAppServices.Metadata.InstanceId);
                span.SetTag(Tags.AzureAppServicesInstanceName, AzureAppServices.Metadata.InstanceName);
                span.SetTag(Tags.AzureAppServicesOperatingSystem, AzureAppServices.Metadata.OperatingSystem);
                span.SetTag(Tags.AzureAppServicesRuntime, AzureAppServices.Metadata.Runtime);
                span.SetTag(Tags.AzureAppServicesExtensionVersion, AzureAppServices.Metadata.SiteExtensionVersion);
            }
        }
    }
}
