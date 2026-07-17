// <copyright file="EventGridFunctionsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    internal static class EventGridFunctionsCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventGridFunctionsCommon));

        /// <summary>
        /// Creates an <c>azure_eventgrid.receive</c> consumer span that links to the producers'
        /// <c>azure_eventgrid.send</c> spans (when W3C contexts were extracted from the CloudEvents),
        /// and returns a <see cref="PropagationContext"/> pointing at the receive span so the
        /// function-invoke span is parented under it. This mirrors the Event Hubs / Service Bus
        /// receive-span topology (new trace, span-linked to the producer).
        /// </summary>
        internal static PropagationContext CreateReceiveSpanContext<T>(Tracer tracer, T context, string? bindingName, Baggage destinationBaggage)
            where T : IFunctionContext
        {
            var cloudEvents = GetCloudEvents(context, bindingName);
            var producerContexts = ExtractPropagatedContexts(cloudEvents);

            // As with Service Bus and Event Hubs batches, use the first extracted producer
            // context as the source of ambient baggage.
            MergeBaggageFromFirstContext(producerContexts, destinationBaggage);
            return CreateReceiveSpan(tracer, cloudEvents, producerContexts);
        }

        internal static List<PropagationContext> ExtractPropagatedContexts(Dictionary<string, object>[] cloudEvents)
        {
            var extractedContexts = new List<PropagationContext>();

            try
            {
                if (cloudEvents.Length == 0)
                {
                    return extractedContexts;
                }

                var uniqueSpanContexts = new HashSet<SpanContext>(new SpanContextComparer());

                foreach (var cloudEventProps in cloudEvents)
                {
                    // Extract W3C trace context and baggage from CloudEvent extension attributes.
                    // These were injected by EventGridCommon.InjectW3CContext() on the publisher side.
                    var extractedContext = AzureMessagingCommon.ExtractContext(cloudEventProps);
                    if (extractedContext.SpanContext is { } spanContext && uniqueSpanContexts.Add(spanContext))
                    {
                        extractedContexts.Add(extractedContext);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated context from EventGrid binding");
            }

            return extractedContexts;
        }

        internal static void MergeBaggageFromFirstContext(IReadOnlyList<PropagationContext> producerContexts, Baggage destination)
        {
            if (producerContexts.Count > 0)
            {
                producerContexts[0].MergeBaggageInto(destination);
            }
        }

        internal static List<SpanLink>? CreateSpanLinks(List<PropagationContext> producerContexts)
        {
            if (producerContexts.Count == 0)
            {
                return null;
            }

            var links = new List<SpanLink>(producerContexts.Count);
            foreach (var producerContext in producerContexts)
            {
                if (producerContext.SpanContext is { } producerSpanContext)
                {
                    links.Add(new SpanLink(producerSpanContext));
                }
            }

            return links;
        }

        /// <summary>
        /// Reads the CloudEvent JSON object or array for the given binding from <c>InputData</c>.
        /// The full CloudEvent JSON (including extension attributes) lives in InputData, not
        /// TriggerMetadata (which only contains <c>{"data": ...}</c> for Event Grid).
        /// </summary>
        internal static Dictionary<string, object>[] GetCloudEvents<T>(T context, string? bindingName)
            where T : IFunctionContext
        {
            if (StringUtil.IsNullOrEmpty(bindingName))
            {
                return [];
            }

            var bindingsFeature = FunctionBindingsCommon.GetBindingsFeature(context);
            if (bindingsFeature is null)
            {
                return [];
            }

            if (bindingsFeature.Value.InputData is null
             || !bindingsFeature.Value.InputData.TryGetValue(bindingName!, out var inputDataObj)
             || inputDataObj is not string inputDataJson)
            {
                return [];
            }

            var firstNonWhitespaceIndex = 0;
            while (firstNonWhitespaceIndex < inputDataJson.Length && char.IsWhiteSpace(inputDataJson[firstNonWhitespaceIndex]))
            {
                firstNonWhitespaceIndex++;
            }

            if (firstNonWhitespaceIndex == inputDataJson.Length)
            {
                return [];
            }

            if (inputDataJson[firstNonWhitespaceIndex] == '[')
            {
                return FunctionBindingsCommon.TryParseJson<Dictionary<string, object>[]>(inputDataJson, out var cloudEventBatch) ? cloudEventBatch : [];
            }

            return FunctionBindingsCommon.TryParseJson<Dictionary<string, object>>(inputDataJson, out var cloudEventProps) ? [cloudEventProps] : [];
        }

        private static PropagationContext CreateReceiveSpan(Tracer tracer, Dictionary<string, object>[] cloudEvents, List<PropagationContext> producerContexts)
        {
            try
            {
                var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureEventGridTags(SpanKinds.Consumer);
                tags.MessagingOperation = "receive";

                var links = CreateSpanLinks(producerContexts);
                var (serviceName, serviceNameSource) = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.AzureEventGrid);

                // Start a new trace (SpanContext.None) so the consumer span links to the producer
                // rather than continuing its trace. The receive span is point-in-time; the function
                // span created later references it by context, just like the cross-process messaging paths.
                using var scope = tracer.StartActiveInternal(
                    "azure_eventgrid.receive",
                    parent: SpanContext.None,
                    tags: tags,
                    serviceName: serviceName,
                    serviceNameSource: serviceNameSource,
                    links: links);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                // A CloudEvent's source identifies where the event originated, not the Event Grid topic.
                // The Functions invocation metadata does not expose the topic, so leave the destination unset.
                span.ResourceName = "eventgrid";

                if (cloudEvents.Length == 1)
                {
                    var cloudEventProps = cloudEvents[0];
                    if (cloudEventProps.TryGetValue("id", out var idObj) && idObj is string id && id.Length > 0)
                    {
                        span.SetTag(Tags.MessagingMessageId, id);
                    }
                }
                else if (cloudEvents.Length > 1)
                {
                    tags.MessagingBatchMessageCount = cloudEvents.Length.ToString();
                }

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventGrid);

                return new PropagationContext(span.Context, baggage: null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating Azure Event Grid receive span");
                return producerContexts.Count == 1 ? producerContexts[0] : default;
            }
        }
    }
}

#endif
