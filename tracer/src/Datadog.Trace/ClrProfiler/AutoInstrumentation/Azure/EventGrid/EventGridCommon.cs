// <copyright file="EventGridCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

internal static class EventGridCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventGridCommon));

    internal static CallTargetState CreateProducerSpan<TTarget>(TTarget instance, IEnumerable? events)
        where TTarget : IEventGridPublisherClient, IDuckType
    {
        var tracer = Tracer.Instance;
        Log.Information("AzureEventGrid OnMethodBegin called. Integration enabled: {Enabled}", tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventGrid));
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventGrid))
        {
            return CallTargetState.GetDefault();
        }

        Scope? scope = null;

        try
        {
            var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureEventGridTags(SpanKinds.Producer);
            tags.MessagingOperation = "send";

            var host = instance.UriBuilder?.Host;
            var topic = GetTopicFromHost(host);
            tags.MessagingDestinationName = topic;

            var messageCount = events is ICollection collection ? collection.Count : 0;
            if (messageCount > 1)
            {
                tags.MessagingBatchMessageCount = messageCount.ToString();
            }

            var (serviceName, serviceNameSource) = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.AzureEventGrid);
            scope = tracer.StartActiveInternal("azure_eventgrid.send", tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
            var span = scope.Span;

            span.Type = SpanTypes.Http;
            span.ResourceName = topic;

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventGrid);
            Log.Information("AzureEventGrid span created: {SpanId}, topic={Topic}", scope.Span.SpanId, topic ?? "null");

            return new CallTargetState(scope);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Azure Event Grid producer span");
            scope?.Dispose();
            return CallTargetState.GetDefault();
        }
    }

    /// <summary>
    /// Extracts the topic name from the Event Grid endpoint host.
    /// The host format is "TOPIC-NAME.REGION.eventgrid.azure.net".
    /// </summary>
    private static string? GetTopicFromHost(string? host)
    {
        if (host is null)
        {
            return null;
        }

        var dotIndex = host.IndexOf('.');
        return dotIndex > 0 ? host.Substring(0, dotIndex) : host;
    }
}
