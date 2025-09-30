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
        private const string OperationName = "azure_eventhubs.send";
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

        internal static CallTargetState CreateSenderSpan<TTarget>(
            TTarget instance,
            IEnumerable? messages = null,
            int? messageCount = null,
            IEnumerable<SpanLink>? spanLinks = null)
            where TTarget : IEventHubProducerClient, IDuckType
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
                tags.MessagingDestinationName = instance.EventHubName;
                tags.MessagingOperation = "send";

                scope = tracer.StartActiveInternal(OperationName, tags: tags, links: spanLinks);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = instance.EventHubName;

                var endpoint = instance.Connection?.ServiceEndpoint;
                if (endpoint != null)
                {
                    tags.NetworkDestinationName = endpoint.Host;
                    var port = endpoint.Port == -1 ? DefaultEventHubsPort : endpoint.Port;
                    tags.NetworkDestinationPort = port.ToString();
                }

                var actualMessageCount = messageCount ?? (messages is ICollection collection ? collection.Count : 0);
                if (actualMessageCount > 1)
                {
                    tags.MessagingBatchMessageCount = actualMessageCount.ToString();
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
