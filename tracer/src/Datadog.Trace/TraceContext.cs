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
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    internal class TraceContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceContext>();

        private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
        private readonly long _timestamp = Stopwatch.GetTimestamp();
        private ArrayBuilder<Span> _spans;

        private int _openSpans;
        private int? _samplingPriority;
        private int? _samplingMechanism;

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

        /// <summary>
        /// Gets the trace's sampling mechanism.
        /// </summary>
        public int? SamplingMechanism
        {
            get => _samplingMechanism;
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
                    DecorateWithAASMetadata(span);

                    if (_samplingPriority == null)
                    {
                        if (span.Context.Parent is SpanContext { SamplingPriority: { } samplingPriority })
                        {
                            // this is a root span created from a propagated context that contains a sampling priority.
                            // no need to track sampling mechanism since we won't override the propagated tag/header.
                            _samplingPriority = samplingPriority;
                            _samplingMechanism = null;
                        }
                        else if (Tracer.Sampler is not null)
                        {
                            // this is a local root span (i.e. not propagated).
                            // determine an initial sampling priority for this trace and track the sampling mechanism.
                            (_samplingPriority, _samplingMechanism) = Tracer.Sampler.MakeSamplingDecision(span);
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
                    AddSamplingMechanismTag(span, _samplingMechanism);
                }
            }

            ArraySegment<Span> spansToWrite = default;

            bool shouldPropagateMetadata = false;

            lock (this)
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
                if (spansToWrite.Array != null)
                {
                    // any span can contain the tags
                    DecorateWithAASMetadata(spansToWrite.Array[0]);
                }

                if (_samplingPriority != null)
                {
                    AddSamplingPriorityTags(spansToWrite, _samplingPriority.Value);
                }
            }

            if (spansToWrite.Count > 0)
            {
                Tracer.Write(spansToWrite);
            }
        }

        public void SetSamplingPriority(int? priority, int? mechanism = null, bool notifyDistributedTracer = true)
        {
            _samplingPriority = priority;
            _samplingMechanism = mechanism;

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

        private static void AddSamplingPriorityTags(ArraySegment<Span> spans, int samplingPriority)
        {
            // The agent looks for the sampling priority on the first span that has no parent
            // Finding those spans is not trivial, so instead we apply the priority to every span

            if (spans.Array != null)
            {
                // Using a for loop to avoid the boxing allocation on ArraySegment.GetEnumerator
                for (int i = 0; i < spans.Count; i++)
                {
                    AddSamplingPriorityTags(spans.Array[i + spans.Offset], samplingPriority);
                }
            }
        }

        private static void AddSamplingMechanismTag(Span span, int? samplingMechanism)
        {
            // set the sampling decision trace tag
            // * we should not overwrite an existing value propagated from upstream service
            // * the "-" prefix is a left-over separator from a previous iteration of this feature (not a typo or a negative sign)
            // * don't set the tag if sampling mechanism is unknown
            if (samplingMechanism is { } mechanism)
            {
                span.Context.TraceContext?.Tags.SetTag(Trace.Tags.Propagated.DecisionMaker, $"-{mechanism}", replaceIfExists: false);
            }
        }

        /// <summary>
        /// When receiving chunks of spans, the backend checks whether the aas.resource.id tag is present on any of the
        /// span to decide which metric to emit (datadog.apm.host.instance or datadog.apm.azure_resource_instance one).
        /// </summary>
        private static void DecorateWithAASMetadata(Span span)
        {
            if (AzureAppServices.Metadata.IsRelevant)
            {
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesSiteName, AzureAppServices.Metadata.SiteName);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesSiteKind, AzureAppServices.Metadata.SiteKind);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesSiteType, AzureAppServices.Metadata.SiteType);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesResourceGroup, AzureAppServices.Metadata.ResourceGroup);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesSubscriptionId, AzureAppServices.Metadata.SubscriptionId);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesResourceId, AzureAppServices.Metadata.ResourceId);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesInstanceId, AzureAppServices.Metadata.InstanceId);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesInstanceName, AzureAppServices.Metadata.InstanceName);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesOperatingSystem, AzureAppServices.Metadata.OperatingSystem);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesRuntime, AzureAppServices.Metadata.Runtime);
                span.SetTag(Datadog.Trace.Tags.AzureAppServicesExtensionVersion, AzureAppServices.Metadata.SiteExtensionVersion);
            }
        }
    }
}
