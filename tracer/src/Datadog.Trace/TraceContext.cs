// <copyright file="TraceContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Globalization;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.Iast;
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
        private readonly object _syncRoot = new();
        private IastRequestContext _iastRequestContext;

        private ArrayBuilder<Span> _spans;
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

        /// <summary>
        /// Gets the iast context.
        /// </summary>
        internal IastRequestContext IastRequestContext
        {
            get
            {
                if (_iastRequestContext == null && Iast.Iast.Instance.Settings.Enabled)
                {
                    lock (_syncRoot)
                    {
                        _iastRequestContext ??= new();
                    }
                }

                return _iastRequestContext;
            }
        }

        private TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp);

        public void AddSpan(Span span)
        {
            lock (_syncRoot)
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

            // Propagate the resource name to the profiler for root web spans
            if (span.IsRootSpan && span.Type == SpanTypes.Web)
            {
                Profiler.Instance.ContextTracker.SetEndpoint(span.RootSpanId, span.ResourceName);

                if (Iast.Iast.Instance.Settings.Enabled)
                {
                    IastRequestContext.AddsIastTagsToSpan(span, _iastRequestContext);
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
                AddAASMetadata(spansToWrite.Array![spansToWrite.Offset]);

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
                AddAASMetadata(spansToWrite.Array![spansToWrite.Offset]);
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
    }
}
