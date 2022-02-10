// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
        /// The original sampling priority value propagated from an upstream service, if any.
        /// The current value may be different and is found in <see cref="_samplingDecision"/>.
        /// </summary>
        private int? _propagatedSamplingPriority;

        /// <summary>
        /// The original value of "_dd.p.upstream_services" propagated from an upstream service, if any.
        /// The current value may be different and is found in <see cref="_traceTags"/>.
        /// </summary>
        private string _propagatedUpstreamServices;

        /// <summary>
        /// The sampling decision made by this service, if any.
        /// Can be different from <see cref="_propagatedSamplingPriority"/>.
        /// </summary>
        private SamplingDecision? _samplingDecision;

        private List<KeyValuePair<string, string>> _traceTags = new(capacity: 1);

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

        public void AddSpan(Span span)
        {
            var isRootSpan = false;

            lock (_globalLock)
            {
                if (RootSpan == null)
                {
                    // first span added is the root span
                    RootSpan = span;

                    // we can do the rest outside of the lock
                    isRootSpan = true;
                }

                _openSpans++;
            }

            if (isRootSpan)
            {
                AddedRootSpan(span);
            }
        }

        private void AddedRootSpan(Span span)
        {
            if (span.Context.Parent is SpanContext parentContext)
            {
                if (parentContext.SamplingPriority != null)
                {
                    // this is a root span whose parent is a propagated context from an upstream service.
                    // save the upstream service's sampling priority, we will need later if it was overridden.
                    _propagatedSamplingPriority = parentContext.SamplingPriority;

                    // keep the same sampling priority as the upstream service
                    // (can be overridden later by AppSec or manually in code by users)
                    SetSamplingDecision(parentContext.SamplingPriority.Value, SamplingMechanism.Propagated);
                }
                else
                {
                    // if there are multiple tracer versions, use the shared sampling decision, if any.
                    // otherwise use the sampler to compute an initial sampling priority for this trace.
                    // (can be overridden later by AppSec or manually in code by users)
                    var samplingDecision = DistributedTracer.Instance.GetSamplingDecision() ??
                                           Tracer.Sampler?.MakeSamplingDecision(RootSpan);

                    SetSamplingDecision(samplingDecision);
                }

                // the collection of Datadog tags is propagated to downstream services via headers, like http
                _traceTags = DatadogTagsHeader.Parse(parentContext.DatadogTags ?? string.Empty);

                // the individual tags are propagated to the Agent as trace-level tags (e.g. as tags on root spans)
                _propagatedUpstreamServices = DatadogTagsHeader.GetTagValue(datadogTags, Tags.Propagated.UpstreamServices);
            }

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

        public void SetSamplingDecision(int priority, int mechanism, float? rate = null, bool notifyDistributedTracer = true, string serviceName = null)
        {
            var samplingDecision = new SamplingDecision(priority, mechanism, rate);
            SetSamplingDecision(samplingDecision, notifyDistributedTracer, serviceName);
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
            // update "_dd.p.upstream_services" and "x-datadog-tags"
            if (samplingDecision != null && samplingDecision.Value.Priority != _propagatedSamplingPriority)
            {
                var upstreamService = new UpstreamService(serviceName ?? Tracer.DefaultServiceName, samplingDecision.Value);
                _upstreamServices = UpstreamService.

                // append a tuple to the "_dd.p.upstream_services" tag in DatadogTags.
                _datadogTags = DatadogTagsHeader.AppendTagValue(DatadogTags, upstreamService);
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
    }
}
