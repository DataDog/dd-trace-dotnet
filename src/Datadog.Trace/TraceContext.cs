using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    internal class TraceContext : ITraceContext
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceContext>();

        private readonly DateTimeOffset _utcStart = DateTimeOffset.UtcNow;
        private readonly long _timestamp = Stopwatch.GetTimestamp();
        private readonly List<Span> _spans = new List<Span>();

        private int _openSpans;
        private SamplingPriority? _samplingPriority;
        private bool _samplingPriorityLocked;

        public TraceContext(IDatadogTracer tracer)
        {
            Tracer = tracer;
        }

        public Span RootSpan { get; private set; }

        public DateTimeOffset UtcNow => _utcStart.Add(Elapsed);

        public IDatadogTracer Tracer { get; }

        /// <summary>
        /// Gets or sets sampling priority.
        /// Once the sampling priority is locked with <see cref="LockSamplingPriority"/>,
        /// further attempts to set this are ignored.
        /// </summary>
        public SamplingPriority? SamplingPriority
        {
            get => _samplingPriority;
            set
            {
                if (!_samplingPriorityLocked)
                {
                    _samplingPriority = value;
                }
            }
        }

        private TimeSpan Elapsed => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp);

        public void AddSpan(Span span)
        {
            lock (_spans)
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
                            LockSamplingPriority();
                        }
                        else
                        {
                            // this is a local root span (i.e. not propagated).
                            // determine an initial sampling priority for this trace, but don't lock it yet
                            _samplingPriority =
                                Tracer.Sampler?.GetSamplingPriority(RootSpan);
                        }
                    }
                }

                _spans.Add(span);
                _openSpans++;
            }
        }

        public void CloseSpan(Span span)
        {
            if (span == RootSpan)
            {
                // lock sampling priority and set metric when root span finishes
                LockSamplingPriority();

                if (_samplingPriority == null)
                {
                    Log.Warning("Cannot set span metric for sampling priority before it has been set.");
                }
                else
                {
                    span.SetMetric(Metrics.SamplingPriority, (int)_samplingPriority);
                }
            }

            Span[] spansToWrite = null;

            lock (_spans)
            {
                _openSpans--;

                if (_openSpans == 0)
                {
                    spansToWrite = _spans.ToArray();
                    _spans.Clear();
                }
            }

            if (spansToWrite != null)
            {
                Tracer.Write(spansToWrite);
            }
        }

        public void LockSamplingPriority()
        {
            if (_samplingPriority == null)
            {
                Log.Warning("Cannot lock sampling priority before it has been set.");
            }
            else
            {
                _samplingPriorityLocked = true;
            }
        }

        public TimeSpan ElapsedSince(DateTimeOffset date)
        {
            return Elapsed + (_utcStart - date);
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

            // set the origin tag to the root span of each trace/subtrace
            if (span.Context.Origin != null)
            {
                span.SetTag(Tags.Origin, span.Context.Origin);
            }
        }
    }
}
