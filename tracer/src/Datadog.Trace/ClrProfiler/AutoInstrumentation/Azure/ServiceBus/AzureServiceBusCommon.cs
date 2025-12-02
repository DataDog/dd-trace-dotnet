// <copyright file="AzureServiceBusCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    internal static class AzureServiceBusCommon
    {
        private static readonly ConditionalWeakTable<object, object?> ApplicationPropertiesToMessageMap = new();

        internal static readonly AsyncLocal<IDictionary<string, object>?> ActiveMessageProperties = new();

        public static void SetMessage(object applicationProperties, object? message)
        {
#if NETCOREAPP3_1_OR_GREATER
            ApplicationPropertiesToMessageMap.AddOrUpdate(applicationProperties, message);
#else
            ApplicationPropertiesToMessageMap.GetValue(applicationProperties, x => message);
#endif
        }

        public static bool TryGetMessage(object applicationProperties, out object? message)
            => ApplicationPropertiesToMessageMap.TryGetValue(applicationProperties, out message);

        internal static long GetMessageSize<T>(T message)
            where T : IServiceBusMessage
        {
            if (message.Instance is null)
            {
                return 0;
            }

            long size = message.Body.ToMemory().Length;

            if (message.ApplicationProperties is null)
            {
                return size;
            }

            foreach (var pair in message.ApplicationProperties)
            {
                size += Encoding.UTF8.GetByteCount(pair.Key);
                size += MessageSizeHelper.TryGetSize(pair.Value);
            }

            return size;
        }

        internal static CallTargetState CreateSenderSpan(
            IServiceBusSender instance,
            string operationName,
            IEnumerable? messages = null,
            int? messageCount = null,
            IEnumerable<SpanLink>? spanLinks = null)
        {
            var entityPath = instance.EntityPath;
            var endpoint = instance.Connection?.ServiceEndpoint;
            var networkDestinationName = endpoint?.Host;
            // https://learn.microsoft.com/en-us/dotnet/api/system.uri.port#remarks
            var networkDestinationPort = endpoint?.Port is null or -1 or 5671 ?
                                            "5671" :
                                            endpoint.Port.ToString();

            return CreateSenderSpanInternal(
                entityPath,
                networkDestinationName,
                networkDestinationPort,
                operationName,
                messages,
                messageCount,
                spanLinks);
        }

        internal static CallTargetState CreateSenderSpan(
            IMessagingClientDiagnostics clientDiagnostics,
            string operationName,
            IEnumerable? messages = null,
            int? messageCount = null,
            IEnumerable<SpanLink>? spanLinks = null)
        {
            var entityPath = clientDiagnostics.EntityPath;
            var networkDestinationName = clientDiagnostics.FullyQualifiedNamespace;

            return CreateSenderSpanInternal(
                entityPath,
                networkDestinationName,
                null,
                operationName,
                messages,
                messageCount,
                spanLinks);
        }

        private static CallTargetState CreateSenderSpanInternal(
            string? entityPath,
            string? networkDestinationName,
            string? networkDestinationPort,
            string operationName,
            IEnumerable? messages,
            int? messageCount,
            IEnumerable<SpanLink>? spanLinks)
        {
            var tracer = Tracer.Instance;
            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
            {
                return new CallTargetState(null);
            }

            var tags = perTraceSettings.Schema.Messaging.CreateAzureServiceBusTags(SpanKinds.Producer);

            tags.MessagingDestinationName = entityPath;
            tags.MessagingOperation = operationName;
            tags.MessagingSystem = "servicebus";
            tags.InstrumentationName = "AzureServiceBus";

            string serviceName = perTraceSettings.Schema.Messaging.GetServiceName("azureservicebus");
            var scope = tracer.StartActiveInternal(
                "azure_servicebus." + operationName,
                tags: tags,
                serviceName: serviceName,
                links: spanLinks);
            var span = scope.Span;

            span.Type = SpanTypes.Queue;
            span.ResourceName = entityPath;

            var actualMessageCount = messageCount ?? (messages is ICollection collection ? collection.Count : 0);
            string? singleMessageId = null;

            if (actualMessageCount > 1)
            {
                span.SetTag(Tags.MessagingBatchMessageCount, actualMessageCount.ToString());
            }

            if (actualMessageCount == 1 && messages != null)
            {
                foreach (var message in messages)
                {
                    var duckTypedMessage = message?.DuckCast<IServiceBusMessage>();
                    singleMessageId = duckTypedMessage?.MessageId;
                    break;
                }

                if (!string.IsNullOrEmpty(singleMessageId))
                {
                    span.SetTag(Tags.MessagingMessageId, singleMessageId);
                }
            }

            if (!string.IsNullOrEmpty(networkDestinationName))
            {
                tags.NetworkDestinationName = networkDestinationName;
            }

            if (!string.IsNullOrEmpty(networkDestinationPort))
            {
                tags.NetworkDestinationPort = networkDestinationPort;
            }

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureServiceBus);

            return new CallTargetState(scope);
        }
    }
}
