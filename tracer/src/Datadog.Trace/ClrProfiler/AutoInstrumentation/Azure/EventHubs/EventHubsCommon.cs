// <copyright file="EventHubsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

internal static class EventHubsCommon
{
    private const int DefaultEventHubsPort = 5671;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubsCommon));

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
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
        {
            return CallTargetState.GetDefault();
        }

        Scope? scope = null;

        try
        {
            var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureEventHubsTags(SpanKinds.Producer);
            tags.MessagingDestinationName = eventHubName;
            tags.MessagingOperation = operationName;

            string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName(MessagingSchema.ServiceType.AzureEventHubs);
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
                    if (message?.TryDuckCast<IEventData>(out var eventData) == true)
                    {
                        singleMessageId = eventData.MessageId;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(singleMessageId))
                {
                    tags.MessagingMessageId = singleMessageId;
                }
            }

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);

            return new CallTargetState(scope);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating producer span");
            scope?.Dispose();
            return CallTargetState.GetDefault();
        }
    }
}
