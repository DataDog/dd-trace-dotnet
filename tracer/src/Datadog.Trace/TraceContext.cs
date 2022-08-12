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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceContext>();

        private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
        private readonly long _timestamp = Stopwatch.GetTimestamp();
        private ArrayBuilder<Span> _spans;

        private int _openSpans;
        private int? _samplingPriority;
        private bool _hasRemoteParent;

        public TraceContext(IDatadogTracer tracer, TraceTagCollection tags = null)
        {
            Tracer = tracer;
            Tags = tags ?? new TraceTagCollection(tracer.Settings.TagPropagationHeaderMaxLength);
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
                    DecorateWithAASMetadata(span);

                    if (_samplingPriority == null)
                    {
                        if (span.Context.Parent is SpanContext { SamplingPriority: { } samplingPriority })
                        {
                            // this is a local root span created from a propagated context that contains a sampling priority.
                            // any distributed tags were extracted from SpanContext.PropagatedTags and added to the TraceContext.
                            _hasRemoteParent = true;
                            _samplingPriority = samplingPriority;
                        }
                        else
                        {
                            // this is a local root span with no upstream service.
                            _hasRemoteParent = false;

                            // make a sampling decision early so it's ready if we need it for propagation.
                            if (Tracer.Sampler is not null)
                            {
                                (_samplingPriority, var samplingMechanism) = Tracer.Sampler.MakeSamplingDecision(span);

                                if (samplingMechanism != null)
                                {
                                    SetSamplingMechanismTag(samplingMechanism.Value);
                                }
                            }
                            else
                            {
                                // fallback to default sampling
                                _samplingPriority = SamplingPriorityValues.AutoKeep;
                                SetSamplingMechanismTag(SamplingMechanism.Default);
                                SetSamplingPriority();
                            }
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
                // any span can contain the tags
                DecorateWithAASMetadata(spansToWrite.Array![0]);

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

            if (priority > 0)
            {
                if (mechanism != null && !_hasRemoteParent)
                {
                    // only set tag is priority is AUTO_KEEP (1) or USER_KEEP (2),
                    // don't set tag if mechanism is unknown (null),
                    // and don't overwrite tag set by upstream service
                    SetSamplingMechanismTag(mechanism.Value);
                }
            }
            else
            {
                RemoveSamplingMechanismTag();
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

        private static void AddSamplingPriorityTags(ArraySegment<Span> spans, int samplingPriority)
        {
            // The agent looks for the sampling priority on the first span that has no parent
            // Finding those spans is not trivial, so instead we apply the priority to every span

            // Using a for loop to avoid the boxing allocation on ArraySegment.GetEnumerator
            for (int i = 0; i < spans.Count; i++)
            {
                AddSamplingPriorityTags(spans.Array![i + spans.Offset], samplingPriority);
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

        private void SetSamplingMechanismTag(int mechanism)
        {
            // set the sampling mechanism trace tag
            // * we should not overwrite an existing value propagated from an upstream service
            // * don't set the tag if sampling mechanism is unknown
            // * the "-" prefix is a left-over separator from a previous iteration of this feature (not a typo or a negative sign)
            const string tagName = Trace.Tags.Propagated.DecisionMaker;
            var tagValue = $"-{mechanism.ToString(CultureInfo.InvariantCulture)}";

            if (_hasRemoteParent)
            {
                // don't override a sampling mechanism received from an upstream service
                Tags.TryAddTag(tagName, tagValue);
            }
            else
            {
                // this is the root service, so we can still change the sampling mechanism
                Tags.SetTag(tagName, tagValue);
            }
        }

        private void RemoveSamplingMechanismTag()
        {
            Tags.SetTag(Trace.Tags.Propagated.DecisionMaker, value: null);
        }
    }
}
