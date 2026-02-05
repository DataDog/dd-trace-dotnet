// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared
{
    internal static class ContextPropagation
    {
        internal const string InjectionKey = "_datadog";

        private static void Inject(Tracer tracer, PropagationContext context, IDictionary messageAttributes, DataStreamsManager? dataStreamsManager, IMessageHeadersHelper messageHeadersHelper)
        {
            // Consolidate headers into one JSON object with <header_name>:<value>
            var sb = Util.StringBuilderCache.Acquire();
            sb.Append('{');
            tracer.TracerManager.SpanContextPropagator.Inject(context, sb, default(StringBuilderCarrierSetter));

            if (context.SpanContext?.PathwayContext is { } pathwayContext)
            {
                dataStreamsManager?.InjectPathwayContext(pathwayContext, AwsMessageAttributesHeadersAdapters.GetInjectionAdapter(sb));
            }

            sb.Remove(startIndex: sb.Length - 1, length: 1); // Remove trailing comma
            sb.Append('}');

            var resultString = Util.StringBuilderCache.GetStringAndRelease(sb);
            messageAttributes[InjectionKey] = messageHeadersHelper.CreateMessageAttributeValue(resultString);
        }

        public static void InjectHeadersIntoMessage(Tracer tracer, IContainsMessageAttributes carrier, SpanContext spanContext, DataStreamsManager? dataStreamsManager, IMessageHeadersHelper messageHeadersHelper)
        {
            // add distributed tracing headers to the message
            if (carrier.MessageAttributes == null)
            {
                carrier.MessageAttributes = messageHeadersHelper.CreateMessageAttributes();
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

            // SNS/SQS allows a maximum of 10 message attributes: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html#sqs-message-attributes
            // Only inject if there's room
            if (carrier.MessageAttributes.Count < 10)
            {
                var context = new PropagationContext(spanContext, Baggage.Current);
                Inject(tracer, context, carrier.MessageAttributes, dataStreamsManager, messageHeadersHelper);
            }
        }

        /// <summary>
        /// Extracts trace context from message attributes if present.
        /// This allows spans to be connected when messaging frameworks like MassTransit
        /// have already injected trace context before the AWS SDK call.
        /// </summary>
        public static PropagationContext ExtractHeadersFromMessage(Tracer tracer, IContainsMessageAttributes? carrier)
        {
            if (carrier?.MessageAttributes == null)
            {
                return default;
            }

            try
            {
                // First try extracting from the _datadog attribute (standard AWS SDK format)
                var datadogAttribute = carrier.MessageAttributes[InjectionKey];
                if (datadogAttribute != null)
                {
                    var messageAttributeValue = datadogAttribute.DuckCast<IMessageAttributeValue>();
                    if (messageAttributeValue != null)
                    {
                        string? jsonString = messageAttributeValue.StringValue;
                        if (!StringUtil.IsNullOrEmpty(jsonString))
                        {
                            var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
                            if (headers != null)
                            {
                                return tracer.TracerManager.SpanContextPropagator
                                             .Extract(headers, default(DictionaryCarrierGetter))
                                             .MergeBaggageInto(Baggage.Current);
                            }
                        }
                    }
                }

                // Fall back to checking individual message attributes for trace headers
                // (e.g., MassTransit may inject headers as separate attributes)
                var headerDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in carrier.MessageAttributes.Keys)
                {
                    if (key is string keyStr)
                    {
                        var attributeValue = carrier.MessageAttributes[keyStr]?.DuckCast<IMessageAttributeValue>();
                        if (attributeValue?.StringValue != null)
                        {
                            headerDict[keyStr] = attributeValue.StringValue;
                        }
                    }
                }

                if (headerDict.Count > 0)
                {
                    var extracted = tracer.TracerManager.SpanContextPropagator
                                          .Extract(headerDict, default(DictionaryCarrierGetter));
                    if (extracted.SpanContext != null)
                    {
                        return extracted.MergeBaggageInto(Baggage.Current);
                    }
                }

                return default;
            }
            catch
            {
                // Ignore extraction errors, will create a new root span
                return default;
            }
        }

        private readonly struct StringBuilderCarrierSetter : ICarrierSetter<StringBuilder>
        {
            public void Set(StringBuilder carrier, string key, string value)
            {
                carrier.AppendFormat("\"{0}\":\"{1}\",", key, value);
            }
        }

        private readonly struct DictionaryCarrierGetter : ICarrierGetter<Dictionary<string, string>>
        {
            public IEnumerable<string> Get(Dictionary<string, string> carrier, string key)
            {
                if (carrier.TryGetValue(key, out var value))
                {
                    return new[] { value };
                }

                return Array.Empty<string>();
            }
        }
    }
}
