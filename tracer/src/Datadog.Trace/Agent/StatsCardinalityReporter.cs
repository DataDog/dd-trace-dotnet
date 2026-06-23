// <copyright file="StatsCardinalityReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent;

internal sealed class StatsCardinalityReporter(IMetricsTelemetryCollector telemetry)
{
    // x2 to handle +/- oversized tags
    private readonly int[] _collapsedSpans = new int[CollapsedStatsFieldsExtensions.Length * 2];
    private readonly IMetricsTelemetryCollector _telemetry = telemetry;
    private readonly IDatadogLogger _logger = DatadogLogging.GetLoggerFor<StatsCardinalityReporter>();

    private int _cardinalityLogWritten;

    public void RecordCardinalityOverflow(StatsCardinalityLimitedFields cardinalityFields, StatsCardinalityTruncatedFields truncatedFields)
    {
        var cardinalityTag = MapTelemetryTags(cardinalityFields);

        var oversizedTag = truncatedFields == StatsCardinalityTruncatedFields.None
                               ? MetricTags.OversizedStatsFields.None
                               : MetricTags.OversizedStatsFields.AdditionalMetricTags;

        _telemetry.RecordCountStatsCollapsedSpans(cardinalityTag, oversizedTag);

        var index = truncatedFields == StatsCardinalityTruncatedFields.None
                        ? (int)cardinalityTag
                        : CollapsedStatsFieldsExtensions.Length + (int)cardinalityTag;

        Interlocked.Increment(ref _collapsedSpans[index]);

        if (Interlocked.Exchange(ref _cardinalityLogWritten, 1) == 0)
        {
            // Write the cardinality exceeded log, once per flush period
            _logger.Debug(
                $"Client-side stats values are being collapsed to '{StatsAggregator.BlockedByTracerSentinel}' in " +
                $"the current flush window. This is caused by a tag value exceeding 200 characters, " +
                "or by exceeding one of the DD_TRACE_STATS_*_CARDINALITY_LIMIT caps.");
        }

        static MetricTags.CollapsedStatsFields MapTelemetryTags(StatsCardinalityLimitedFields fields)
            => fields switch
            {
                StatsCardinalityLimitedFields.None => MetricTags.CollapsedStatsFields.None,
                StatsCardinalityLimitedFields.WholeKey | StatsCardinalityLimitedFields.All => MetricTags.CollapsedStatsFields.WholeKey,
                StatsCardinalityLimitedFields.Resource => MetricTags.CollapsedStatsFields.Resource,
                StatsCardinalityLimitedFields.HttpEndpoint => MetricTags.CollapsedStatsFields.HttpEndpoint,
                StatsCardinalityLimitedFields.PeerTags => MetricTags.CollapsedStatsFields.PeerTags,
                StatsCardinalityLimitedFields.AdditionalMetricTags => MetricTags.CollapsedStatsFields.AdditionalMetricTags,
                StatsCardinalityLimitedFields.Resource | StatsCardinalityLimitedFields.HttpEndpoint => MetricTags.CollapsedStatsFields.ResourceAndHttpEndpoint,
                StatsCardinalityLimitedFields.Resource | StatsCardinalityLimitedFields.PeerTags => MetricTags.CollapsedStatsFields.ResourceAndPeerTags,
                StatsCardinalityLimitedFields.Resource | StatsCardinalityLimitedFields.AdditionalMetricTags => MetricTags.CollapsedStatsFields.ResourceAndAdditionalMetricTags,
                StatsCardinalityLimitedFields.HttpEndpoint | StatsCardinalityLimitedFields.PeerTags => MetricTags.CollapsedStatsFields.HttpEndpointAndPeerTags,
                StatsCardinalityLimitedFields.HttpEndpoint | StatsCardinalityLimitedFields.AdditionalMetricTags => MetricTags.CollapsedStatsFields.HttpEndpointAndAdditionalMetricTags,
                StatsCardinalityLimitedFields.PeerTags | StatsCardinalityLimitedFields.AdditionalMetricTags => MetricTags.CollapsedStatsFields.PeerTagsAndAdditionalMetricTags,
                StatsCardinalityLimitedFields.Resource | StatsCardinalityLimitedFields.HttpEndpoint | StatsCardinalityLimitedFields.PeerTags => MetricTags.CollapsedStatsFields.ResourceAndHttpEndpointAndPeerTags,
                StatsCardinalityLimitedFields.Resource | StatsCardinalityLimitedFields.HttpEndpoint | StatsCardinalityLimitedFields.AdditionalMetricTags => MetricTags.CollapsedStatsFields.ResourceAndHttpEndpointAndAdditionalMetricTags,
                StatsCardinalityLimitedFields.Resource | StatsCardinalityLimitedFields.PeerTags | StatsCardinalityLimitedFields.AdditionalMetricTags => MetricTags.CollapsedStatsFields.ResourceAndPeerTagsAndAdditionalMetricTags,
                StatsCardinalityLimitedFields.HttpEndpoint | StatsCardinalityLimitedFields.PeerTags | StatsCardinalityLimitedFields.AdditionalMetricTags => MetricTags.CollapsedStatsFields.HttpEndpointAndPeerTagsAndAdditionalMetricTags,
                StatsCardinalityLimitedFields.Resource | StatsCardinalityLimitedFields.HttpEndpoint | StatsCardinalityLimitedFields.PeerTags | StatsCardinalityLimitedFields.AdditionalMetricTags => MetricTags.CollapsedStatsFields.ResourceAndHttpEndpointAndPeerTagsAndAdditionalMetricTags,
                _ => MetricTags.CollapsedStatsFields.None,
            };
    }

    public void EmitCollapsedHealthCheckMetrics(IDogStatsd statsd)
    {
        for (var i = 0; i < _collapsedSpans.Length; i++)
        {
            var count = _collapsedSpans[i];
            if (count > 0)
            {
                statsd.Counter(TracerMetricNames.Stats.CollapsedSpans, value: count, tags: StatsdTags.Tags[i]);
            }
        }
    }

    public void Reset()
    {
#if NET6_0_OR_GREATER
        Array.Clear(_collapsedSpans);
#else
        Array.Clear(_collapsedSpans, 0, _collapsedSpans.Length);
#endif
        Interlocked.Exchange(ref _cardinalityLogWritten, 0);
    }

    // These are in a separate class to avoid triggering the array allocations if we're not going to need them
    private static class StatsdTags
    {
        private const string WholeKeyTag = "collapsed:whole-key";
        private const string ResourceTag = "collapsed:resource";
        private const string HttpEndpointTag = "collapsed:http_endpoint";
        private const string PeerTagsTag = "collapsed:peer_tags";
        private const string AdditionalMetricTagsTag = "collapsed:additional_metric_tags";
        private const string OversizedAdditionalMetricTagsTag = "oversized:additional_metric_tags";

        // These must stay in sync with MetricTags.CollapsedStatsFields with +/- oversized tag
        internal static readonly string[][] Tags =
        [
            // OversizedStatsFields.None
            [],
            [WholeKeyTag],

            [ResourceTag],
            [HttpEndpointTag],
            [PeerTagsTag],
            [AdditionalMetricTagsTag],

            [ResourceTag, HttpEndpointTag],
            [ResourceTag, PeerTagsTag],
            [ResourceTag, AdditionalMetricTagsTag],
            [HttpEndpointTag, PeerTagsTag],
            [HttpEndpointTag, AdditionalMetricTagsTag],
            [PeerTagsTag, AdditionalMetricTagsTag],

            [ResourceTag, HttpEndpointTag, PeerTagsTag],
            [ResourceTag, HttpEndpointTag, AdditionalMetricTagsTag],
            [ResourceTag, PeerTagsTag, AdditionalMetricTagsTag],
            [HttpEndpointTag, PeerTagsTag, AdditionalMetricTagsTag],

            [ResourceTag, HttpEndpointTag, PeerTagsTag, AdditionalMetricTagsTag],

            // OversizedStatsFields.AdditionalMetricTags
            [OversizedAdditionalMetricTagsTag],
            [OversizedAdditionalMetricTagsTag, WholeKeyTag],

            [OversizedAdditionalMetricTagsTag, ResourceTag],
            [OversizedAdditionalMetricTagsTag, HttpEndpointTag],
            [OversizedAdditionalMetricTagsTag, PeerTagsTag],
            [OversizedAdditionalMetricTagsTag, AdditionalMetricTagsTag],

            [OversizedAdditionalMetricTagsTag, ResourceTag, HttpEndpointTag],
            [OversizedAdditionalMetricTagsTag, ResourceTag, PeerTagsTag],
            [OversizedAdditionalMetricTagsTag, ResourceTag, AdditionalMetricTagsTag],
            [OversizedAdditionalMetricTagsTag, HttpEndpointTag, PeerTagsTag],
            [OversizedAdditionalMetricTagsTag, HttpEndpointTag, AdditionalMetricTagsTag],
            [OversizedAdditionalMetricTagsTag, PeerTagsTag, AdditionalMetricTagsTag],

            [OversizedAdditionalMetricTagsTag, ResourceTag, HttpEndpointTag, PeerTagsTag],
            [OversizedAdditionalMetricTagsTag, ResourceTag, HttpEndpointTag, AdditionalMetricTagsTag],
            [OversizedAdditionalMetricTagsTag, ResourceTag, PeerTagsTag, AdditionalMetricTagsTag],
            [OversizedAdditionalMetricTagsTag, HttpEndpointTag, PeerTagsTag, AdditionalMetricTagsTag],

            [OversizedAdditionalMetricTagsTag, ResourceTag, HttpEndpointTag, PeerTagsTag, AdditionalMetricTagsTag],
        ];
    }
}
