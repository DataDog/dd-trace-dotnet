// <copyright file="NullStatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent
{
    internal sealed class NullStatsAggregator : IStatsAggregator
    {
        public bool? CanComputeStats => false;

        public void Add(params Span[] spans)
        {
        }

        public void AddRange(in SpanCollection spans)
        {
        }

        public TraceKeepState ProcessTrace(ref SpanCollection spans) => TraceKeepState.AggregateAndExport;

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public StatsAggregationKey BuildKey(Span span, out List<byte[]> utf8PeerTags)
        {
            utf8PeerTags = [];

            var rawHttpStatusCode = span.GetTag(Tags.HttpStatusCode);
            if (rawHttpStatusCode is null || !int.TryParse(rawHttpStatusCode, out var httpStatusCode))
            {
                httpStatusCode = 0;
            }

            return new StatsAggregationKey(
                span.ResourceName,
                span.ServiceName,
                span.OperationName,
                span.Type,
                httpStatusCode,
                isSyntheticsRequest: span.Context.Origin?.StartsWith("synthetics") == true,
                spanKind: string.Empty,
                isError: false,
                isTopLevel: false,
                isTraceRoot: false,
                httpMethod: string.Empty,
                httpEndpoint: string.Empty,
                grpcStatusCode: string.Empty,
                serviceSource: string.Empty,
                peerTagsHash: 0);
        }
    }
}
