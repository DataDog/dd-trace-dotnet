// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Tagging.PropagatedTags;
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

        /// <summary>
        /// The sampling priority propagated from an upstream service, if any.
        /// </summary>
        private int? _propagatedSamplingPriority;

        // / <summary>
        // / The upstream services data propagated from an upstream service, if any.
        // / </summary>
        // private string _propagatedUpstreamServices;

        // the sampling decision made by this service, if any
        private SamplingDecision? _samplingDecision;

        public TraceContext(IDatadogTracer tracer)
        {
            Tracer = tracer;
        }

        public Span RootSpan { get; private set; }

        public DateTimeOffset UtcNow => _utcStart.Add(Elapsed);

        public IDatadogTracer Tracer { get; }

        /// <summary>
        /// Gets this trace's sampling decision.
        /// </summary>
        public SamplingDecision? SamplingDecision => _samplingDecision;

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

                    if (SamplingDecision == null)
                    {
                        SamplingDecision? samplingDecision = null;

                        if (span.Context.Parent is SpanContext spanContext)
                        {
                            if (spanContext.SamplingPriority != null)
                            {
                                // this is a root span whose parent is a propagated context from an upstream service.
                                // save the upstream service's sampling priority, we will need later.
                                _propagatedSamplingPriority = spanContext.SamplingPriority.Value;

                                // keep the same sampling priority as the upstream service
                                // (can be overridden later by AppSec or manually in code by users)
                                samplingDecision = new SamplingDecision(spanContext.SamplingPriority.Value, SamplingMechanism.Propagated);
                            }
                        }
                        else
                        {
                            // if there are multiple tracer versions, use the shared sampling decision, if any.
                            // otherwise compute an initial sampling priority for this trace.
                            // (can be overridden later by AppSec or manually in code by users)
                            samplingDecision = DistributedTracer.Instance.GetSamplingDecision() ??
                                               Tracer.Sampler?.MakeSamplingDecision(RootSpan);
                        }

                        if (samplingDecision != null)
                        {
                            SetSamplingDecision(samplingDecision);
                        }
                    }
                }

                // TODO: _propagatedUpstreamServices = DatadogTagsHeader.GetTagValue(spanContext.DatadogTags, Tags.Propagated.UpstreamServices);
                // TODO: DatadogTags =

                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            bool ShouldTriggerPartialFlush() => Tracer.Settings.Exporter.PartialFlushEnabled && _spans.Count >= Tracer.Settings.Exporter.PartialFlushMinSpans;

            if (span == RootSpan)
            {
                var samplingPriority = _samplingDecision?.Priority;

                if (samplingPriority == null)
                {
                    Log.Warning("Cannot set span metric for sampling priority before it has been set.");
                }
                else
                {
                    AddSamplingPriorityTags(span, samplingPriority.Value);
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

        public void SetSamplingDecision(int priority, int mechanism, float? rate = null, bool notifyDistributedTracer = true)
        {
            var samplingDecision = new SamplingDecision(priority, mechanism, rate);
            SetSamplingDecision(samplingDecision, notifyDistributedTracer);
        }

        public void SetSamplingDecision(SamplingDecision? samplingDecision, bool notifyDistributedTracer = true, string serviceName = null)
        {
            _samplingDecision = samplingDecision;

            if (notifyDistributedTracer)
            {
                DistributedTracer.Instance.SetSamplingDecision(samplingDecision);
            }

            // if there was no sampling priority value from upstream,
            // or if this service changed the sampling priority from the upstream value,
            // append a tuple to the "_dd.p.upstream_services" tag in DatadogTags.
            if (samplingDecision != null && samplingDecision.Value.Priority != _propagatedSamplingPriority)
            {
                var upstreamService = new UpstreamService(serviceName ?? Tracer.DefaultServiceName, samplingDecision.Value);
                // TODO: DatadogTags = DatadogTagsHeader.AppendTagValue(DatadogTags, upstreamService);
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
                // TODO: tags.UpstreamService = new UpstreamService(Tracer.DefaultServiceName, samplingDecision.Value).ToString();
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

            if (SamplingDecision == null)
            {
                return;
            }

            var samplingPriority = SamplingDecision.Value.Priority;

            // Using a for loop to avoid the boxing allocation on ArraySegment.GetEnumerator
            for (int i = 0; i < spans.Count; i++)
            {
                AddSamplingPriorityTags(spans.Array[i + spans.Offset], samplingPriority);
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
