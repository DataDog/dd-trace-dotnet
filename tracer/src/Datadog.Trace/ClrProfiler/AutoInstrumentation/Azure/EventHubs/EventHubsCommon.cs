// <copyright file="EventHubsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    internal static class EventHubsCommon
    {
        private const int DefaultEventHubsPort = 5671;
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubsCommon));

        // Maps EventDataBatch instances to their collection of message span contexts
        private static readonly ConditionalWeakTable<object, ConcurrentBag<SpanContext>> BatchToSpanContexts = new();

        public static void StoreSpanContext(object batchInstance, SpanContext spanContext)
        {
            if (batchInstance == null || spanContext == null)
            {
                return;
            }

            try
            {
                var spanContexts = BatchToSpanContexts.GetValue(batchInstance, _ => new ConcurrentBag<SpanContext>());
                spanContexts.Add(spanContext);

                Log.Debug(LogPrefix + "Stored span context for batch. TraceId={TraceId}, SpanId={SpanId}", (object)spanContext.TraceId128, (object)spanContext.SpanId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, LogPrefix + "Failed to store span context for EventDataBatch");
            }
        }

        public static IEnumerable<SpanContext>? RetrieveAndClearSpanContexts(object? batchInstance)
        {
            if (batchInstance == null)
            {
                return null;
            }

            try
            {
                if (!BatchToSpanContexts.TryGetValue(batchInstance, out var spanContexts) || spanContexts.IsEmpty)
                {
                    Log.Debug(LogPrefix + "No stored span contexts found for batch");
                    return null;
                }

                var contexts = spanContexts.ToList();

                Log.Debug(LogPrefix + "Retrieved {Count} span contexts for batch send operation", (object)contexts.Count);

                BatchToSpanContexts.Remove(batchInstance);

                return contexts.Count > 0 ? contexts : null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, LogPrefix + "Failed to retrieve span contexts for EventDataBatch");
                return null;
            }
        }

        internal static CallTargetState CreateSenderSpan(
            IEventHubProducerClient instance,
            string operationName,
            IEnumerable? messages = null,
            int? messageCount = null,
            IEnumerable<SpanLink>? spanLinks = null)
        {
            var endpoint = instance.Connection?.ServiceEndpoint;
            var networkDestinationName = endpoint?.Host;
            var networkDestinationPort = endpoint?.Port is null or -1 or 5671 ?
                                            "5671" :
                                            endpoint.Port.ToString();

            return CreateSenderSpanInternal(
                instance.EventHubName,
                networkDestinationName,
                networkDestinationPort,
                operationName,
                messages,
                messageCount,
                spanLinks);
        }

        internal static CallTargetState CreateSenderSpan(
            IEventDataBatch instance,
            string operationName,
            IEnumerable? messages = null,
            int? messageCount = null,
            IEnumerable<SpanLink>? spanLinks = null)
        {
            var networkDestinationName = instance.FullyQualifiedNamespace;

            return CreateSenderSpanInternal(
                instance.EventHubName,
                networkDestinationName,
                null,
                operationName,
                messages,
                messageCount,
                spanLinks);
        }

        private static CallTargetState CreateSenderSpanInternal(
            string? eventHubName,
            string? networkDestinationName,
            string? networkDestinationPort,
            string operationName,
            IEnumerable? messages,
            int? messageCount,
            IEnumerable<SpanLink>? spanLinks)
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
            {
                return CallTargetState.GetDefault();
            }

            Scope? scope = null;

            try
            {
                var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureEventHubsTags(SpanKinds.Producer);
                tags.MessagingDestinationName = eventHubName;
                tags.MessagingOperation = operationName;

                string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName("azureeventhubs");
                scope = tracer.StartActiveInternal("azure_eventhubs." + operationName, tags: tags, serviceName: serviceName, links: spanLinks);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = eventHubName;

                if (!string.IsNullOrEmpty(networkDestinationName))
                {
                    tags.NetworkDestinationName = networkDestinationName;
                }

                if (!string.IsNullOrEmpty(networkDestinationPort))
                {
                    tags.NetworkDestinationPort = networkDestinationPort;
                }

                var actualMessageCount = messageCount ?? (messages is ICollection collection ? collection.Count : 0);
                string? singleMessageId = null;

                if (actualMessageCount > 1)
                {
                    tags.MessagingBatchMessageCount = actualMessageCount.ToString();
                }

                if (actualMessageCount == 1 && messages != null)
                {
                    foreach (var message in messages)
                    {
                        var duckTypedMessage = message?.DuckCast<IEventData>();
                        singleMessageId = duckTypedMessage?.MessageId;
                        break;
                    }

                    if (!string.IsNullOrEmpty(singleMessageId))
                    {
                        tags.MessagingMessageId = singleMessageId;
                    }
                }

                return new CallTargetState(scope);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error creating producer span");
                scope?.Dispose();
                return CallTargetState.GetDefault();
            }
        }

        internal static TReturn OnAsyncMethodEnd<TReturn>(TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return returnValue;
        }
    }
}
