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
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared
{
    internal static class ContextPropagation
    {
        internal const string InjectionKey = "_datadog";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagation));

        private static void Inject(PropagationContext context, IDictionary messageAttributes, DataStreamsManager? dataStreamsManager, IMessageHeadersHelper messageHeadersHelper)
        {
            Console.WriteLine("ContextPropagation.Inject: Starting context injection. DataStreamsManager enabled: {0}", dataStreamsManager?.IsEnabled);

            // Consolidate headers into one JSON object with <header_name>:<value>
            var sb = Util.StringBuilderCache.Acquire();
            sb.Append('{');
            Tracer.Instance.TracerManager.SpanContextPropagator.Inject(context, sb, default(StringBuilderCarrierSetter));
            Console.WriteLine("ContextPropagation.Inject: Injected span context into StringBuilder");

            if (context.SpanContext?.PathwayContext is { } pathwayContext)
            {
                Console.WriteLine("ContextPropagation.Inject: Found pathway context, injecting into message attributes");
                dataStreamsManager?.InjectPathwayContext(pathwayContext, AwsMessageAttributesHeadersAdapters.GetInjectionAdapter(sb));
                Console.WriteLine("ContextPropagation.Inject: Injected pathway context");
            }
            else
            {
                Console.WriteLine("ContextPropagation.Inject: No pathway context found in span context");
            }

            sb.Remove(startIndex: sb.Length - 1, length: 1); // Remove trailing comma
            sb.Append('}');

            var resultString = Util.StringBuilderCache.GetStringAndRelease(sb);
            Console.WriteLine("ContextPropagation.Inject: Created JSON string with length: {0}", resultString.Length);

            messageAttributes[InjectionKey] = messageHeadersHelper.CreateMessageAttributeValue(resultString);
            Console.WriteLine("ContextPropagation.Inject: Added context to message attributes under key: {0}", InjectionKey);
        }

        public static void InjectHeadersIntoMessage(IContainsMessageAttributes carrier, SpanContext spanContext, DataStreamsManager? dataStreamsManager, IMessageHeadersHelper messageHeadersHelper)
        {
            Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: Starting header injection. DataStreamsManager enabled: {0}", dataStreamsManager?.IsEnabled);

            // add distributed tracing headers to the message
            if (carrier.MessageAttributes == null)
            {
                Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: MessageAttributes is null, creating new dictionary");
                carrier.MessageAttributes = messageHeadersHelper.CreateMessageAttributes();
            }
            else
            {
                Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: MessageAttributes count before cleanup: {0}", carrier.MessageAttributes.Count);
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
                        Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: Found existing Datadog attribute to remove: {0}", attributeName);
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
                    Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: Removing {0} existing Datadog attributes", attributesToRemove.Count);
                    foreach (var attribute in attributesToRemove)
                    {
                        carrier.MessageAttributes.Remove(attribute);
                    }
                }
#endif
                Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: MessageAttributes count after cleanup: {0}", carrier.MessageAttributes.Count);
            }

            // SNS/SQS allows a maximum of 10 message attributes: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html#sqs-message-attributes
            // Only inject if there's room
            if (carrier.MessageAttributes.Count < 10)
            {
                Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: MessageAttributes count ({0}) < 10, proceeding with injection", carrier.MessageAttributes.Count);
                var context = new PropagationContext(spanContext, Baggage.Current);
                Inject(context, carrier.MessageAttributes, dataStreamsManager, messageHeadersHelper);
                Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: Successfully injected context. Final count: {0}", carrier.MessageAttributes.Count);
            }
            else
            {
                Console.WriteLine("ContextPropagation.InjectHeadersIntoMessage: MessageAttributes count ({0}) >= 10, skipping injection", carrier.MessageAttributes.Count);
            }
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
