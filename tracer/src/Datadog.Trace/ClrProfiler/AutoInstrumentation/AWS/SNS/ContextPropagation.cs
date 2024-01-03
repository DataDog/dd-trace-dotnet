// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    internal static class ContextPropagation
    {
        private const string SnsKey = "_datadog";

        private static void Inject<TMessageRequest>(SpanContext context, IDictionary messageAttributes)
        {
            // Consolidate headers into one JSON object with <header_name>:<value>
            var sb = Util.StringBuilderCache.Acquire(Util.StringBuilderCache.MaxBuilderSize);
            sb.Append('{');
            SpanContextPropagator.Instance.Inject(context, sb, default(StringBuilderCarrierSetter));
            sb.Remove(startIndex: sb.Length - 1, length: 1); // Remove trailing comma
            sb.Append('}');

            var resultString = Util.StringBuilderCache.GetStringAndRelease(sb);
            var bytes = Encoding.UTF8.GetBytes(resultString);
            var stream = new MemoryStream(bytes);
            messageAttributes[SnsKey] = CachedMessageHeadersHelper<TMessageRequest>.CreateMessageAttributeValue(stream);
        }

        public static void InjectHeadersIntoBatch<TClientMarker, TBatchRequest>(TBatchRequest request, SpanContext context)
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
                    InjectHeadersIntoMessage<TClientMarker, IContainsMessageAttributes>(entry, context);
                }
            }
        }

        public static void InjectHeadersIntoMessage<TClientMarker, TMessageRequest>(TMessageRequest carrier, SpanContext context)
            where TMessageRequest : IContainsMessageAttributes
        {
            // Skip adding Trace Context if there is no more space left to inject.
            // AWS SNS Message Attributes limit is 10.
            if (carrier.MessageAttributes is { Count: >= 10 })
            {
                return;
            }

            // Add distributed tracing headers to the message.
            if (carrier.MessageAttributes == null)
            {
                carrier.MessageAttributes = CachedMessageHeadersHelper<TClientMarker>.CreateMessageAttributes();
            }
            else
            {
                // In .NET Fx and Net Core 2.1, removing an element while iterating on keys throws.
#if !NETCOREAPP2_1_OR_GREATER
                List<string>? attributesToRemove = null;
#endif
                // Make sure we do not propagate any other datadog header here in the rare cases where users would have added them manually
                foreach (var attribute in carrier.MessageAttributes.Keys)
                {
                    if (attribute is string attributeName &&
                        (attributeName.StartsWith("x-datadog", StringComparison.OrdinalIgnoreCase)
                            || attributeName.Equals(DataStreamsPropagationHeaders.PropagationKey, StringComparison.OrdinalIgnoreCase)))
                    {
#if !NETCOREAPP2_1_OR_GREATER
                        attributesToRemove ??= new List<string>();
                        attributesToRemove.Add(attributeName);
#else
                        carrier.MessageAttributes.Remove(attribute);
#endif
                    }
                }

#if !NETCOREAPP2_1_OR_GREATER
                if (attributesToRemove != null)
                {
                    foreach (var attribute in attributesToRemove)
                    {
                        carrier.MessageAttributes.Remove(attribute);
                    }
                }
#endif
            }

            // Inject the tracing headers
            Inject<TClientMarker>(context, carrier.MessageAttributes);
        }

        private readonly struct StringBuilderCarrierSetter : ICarrierSetter<StringBuilder>
        {
            public void Set(StringBuilder carrier, string key, string value)
            {
                carrier.AppendFormat("\"{0}\":\"{1}\",", key, value);
            }
        }
    }
}
