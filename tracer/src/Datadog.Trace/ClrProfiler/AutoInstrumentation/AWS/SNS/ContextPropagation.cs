// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    internal static class ContextPropagation
    {
        private const string SnsKey = "_datadog";

        public static void InjectHeadersIntoBatch<TBatchRequest>(TBatchRequest request, SpanContext context)
            where TBatchRequest : IPublishBatchRequest
        {
            // Skip adding Trace Context if entries don't exist or empty.
            if (request.PublishBatchRequestEntries is not { Count: > 0 })
            {
                return;
            }

            foreach (var t in request.PublishBatchRequestEntries)
            {
                var entry = t?.DuckCast<IContainsMessageAttributes>();

                if (entry != null)
                {
                    Shared.ContextPropagation.InjectHeadersIntoMessage(entry, context, dataStreamsManager: null, CachedMessageHeadersHelper<TBatchRequest>.Instance);
                }
            }
        }
    }
}
