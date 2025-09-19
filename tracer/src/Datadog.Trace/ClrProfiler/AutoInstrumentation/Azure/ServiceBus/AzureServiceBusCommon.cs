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
    internal class AzureServiceBusCommon
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

        internal static CallTargetState CreateSenderSpan<TTarget>(TTarget instance, string operationName = "azure_servicebus.send", IEnumerable? messages = null)
            where TTarget : IServiceBusSender, IDuckType
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus, false))
            {
                return new CallTargetState(null);
            }

            var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureServiceBusTags(SpanKinds.Producer);

            var entityPath = instance.EntityPath ?? "unknown";
            tags.MessagingDestinationName = entityPath;
            tags.MessagingOperation = "send";
            tags.MessagingSystem = "servicebus";
            tags.InstrumentationName = "AzureServiceBus";

            string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName("azureservicebus");
            var scope = tracer.StartActiveInternal(
                operationName,
                tags: tags,
                serviceName: serviceName);
            var span = scope.Span;

            span.Type = SpanTypes.Queue;
            span.ResourceName = entityPath;

            var messageCount = messages is ICollection collection ? collection.Count : 0;
            string? singleMessageId = null;

            if (messageCount > 1)
            {
                span.SetTag(Tags.MessagingBatchMessageCount, messageCount.ToString());
            }

            if (messageCount == 1 && messages != null)
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

            var endpoint = instance.Connection?.ServiceEndpoint;
            if (endpoint != null)
            {
                tags.NetworkDestinationName = endpoint.Host;
                // https://learn.microsoft.com/en-us/dotnet/api/system.uri.port?view=net-8.0#remarks
                tags.NetworkDestinationPort = endpoint.Port is -1 or 5671 ?
                                    "5671" :
                                    endpoint.Port.ToString();
            }

            return new CallTargetState(scope);
        }
    }
}
