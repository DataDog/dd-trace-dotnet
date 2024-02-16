// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    internal static class ContextPropagation
    {
        internal const string SqsKey = "_datadog";

        private static void Inject<TMessageRequest>(SpanContext context, IDictionary messageAttributes, DataStreamsManager dataStreamsManager)
        {
            // Consolidate headers into one JSON object with <header_name>:<value>
            var sb = Util.StringBuilderCache.Acquire(Util.StringBuilderCache.MaxBuilderSize);
            sb.Append('{');
            SpanContextPropagator.Instance.Inject(context, sb, default(StringBuilderCarrierSetter));
            dataStreamsManager?.InjectPathwayContext(context.PathwayContext, new StringBuilderJsonAdapter(sb));
            sb.Remove(startIndex: sb.Length - 1, length: 1); // Remove trailing comma
            sb.Append('}');

            var resultString = Util.StringBuilderCache.GetStringAndRelease(sb);
            messageAttributes[SqsKey] = CachedMessageHeadersHelper<TMessageRequest>.CreateMessageAttributeValue(resultString);
        }

        public static void InjectHeadersIntoMessage<TMessageRequest>(IContainsMessageAttributes carrier, SpanContext spanContext, DataStreamsManager dataStreamsManager)
        {
            // add distributed tracing headers to the message
            if (carrier.MessageAttributes == null)
            {
                carrier.MessageAttributes = CachedMessageHeadersHelper<TMessageRequest>.CreateMessageAttributes();
            }
            else
            {
                // In .NET Fx and Net Core 2.1, removing an element while iterating on keys throws.
#if !NETCOREAPP2_1_OR_GREATER
                List<string> attributesToRemove = null;
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

            // SQS allows a maximum of 10 message attributes: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html#sqs-message-attributes
            // Only inject if there's room
            if (carrier.MessageAttributes.Count < 10)
            {
                Inject<TMessageRequest>(spanContext, carrier.MessageAttributes, dataStreamsManager);
            }
        }

        private readonly struct StringBuilderCarrierSetter : ICarrierSetter<StringBuilder>
        {
            public void Set(StringBuilder carrier, string key, string value)
            {
                carrier.AppendFormat("\"{0}\":\"{1}\",", key, value);
            }
        }

        /// <summary>
        /// The adapter to use to append stuff to a string builder where a json is being built
        /// </summary>
        private readonly struct StringBuilderJsonAdapter : IBinaryHeadersCollection
        {
            private readonly StringBuilder _carrier;

            public StringBuilderJsonAdapter(StringBuilder carrier)
            {
                _carrier = carrier;
            }

            public byte[] TryGetLastBytes(string name)
            {
                throw new NotImplementedException("this adapter can only be use to write to a StringBuilder, not to read data");
            }

            public void Add(string key, byte[] value)
            {
                _carrier
                    .Append('"')
                    .Append(key)
                    .Append("\":\"")
                    .Append(Convert.ToBase64String(value))
                    .Append("\",");
            }
        }

        /// <summary>
        /// The adapter to use to read attributes packed in a json string under the _datadog key
        /// </summary>
        public readonly struct MessageAttributesAdapter : IBinaryHeadersCollection
        {
            private readonly IDictionary _messageAttributes;

            public MessageAttributesAdapter(IDictionary messageAttributes)
            {
                _messageAttributes = messageAttributes;
            }

            public byte[] TryGetLastBytes(string name)
            {
                // IDictionary returns null if the key is not present
                var json = _messageAttributes?[SqsKey]?.DuckCast<IMessageAttributeValue>();
                if (json != null)
                {
                    var ddAttributes = JsonConvert.DeserializeObject<Dictionary<string, string>>(json.StringValue);
                    if (ddAttributes.TryGetValue(name, out var b64))
                    {
                        return Convert.FromBase64String(b64);
                    }
                }

                return Array.Empty<byte>();
            }

            public void Add(string name, byte[] value)
            {
                throw new NotImplementedException("this is meant to read attributes only, not write them");
            }
        }
    }
}
