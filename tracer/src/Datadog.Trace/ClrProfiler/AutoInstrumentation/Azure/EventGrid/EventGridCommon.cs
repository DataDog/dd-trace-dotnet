// <copyright file="EventGridCommon.cs" company="Datadog">
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
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

internal static class EventGridCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventGridCommon));

    internal static CallTargetState CreateProducerSpan<TTarget>(TTarget instance, IEnumerable? events)
        where TTarget : IEventGridPublisherClient, IDuckType
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventGrid))
        {
            return CallTargetState.GetDefault();
        }

        Scope? scope = null;

        try
        {
            var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureEventGridTags(SpanKinds.Producer);
            tags.MessagingOperation = "send";

            var uriBuilder = instance.UriBuilder;
            var host = uriBuilder?.Host;
            var topic = GetTopicFromHost(host);
            tags.MessagingDestinationName = topic;
            tags.NetworkDestinationName = host;

            var port = uriBuilder?.Port ?? -1;
            if (port is not -1)
            {
                tags.NetworkDestinationPort = port.ToString();
            }

            var messageCount = events is ICollection collection ? collection.Count : 0;
            if (messageCount > 1)
            {
                tags.MessagingBatchMessageCount = messageCount.ToString();
            }

            var (serviceName, serviceNameSource) = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.AzureEventGrid);
            scope = tracer.StartActiveInternal("azure_eventgrid.send", tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
            var span = scope.Span;

            span.Type = SpanTypes.Queue;
            span.ResourceName = topic;

            if (events is not null)
            {
                foreach (var evt in events)
                {
                    if (evt is null)
                    {
                        continue;
                    }

                    if (messageCount == 1 && evt.DuckCast<IEventGridEventId>() is { Id: { } id } && id.Length > 0)
                    {
                        span.SetTag(Tags.MessagingMessageId, id);
                    }

                    // Inject W3C trace context into CloudEvent ExtensionAttributes.
                    // CloudEvent extension attribute names only allow [a-z0-9], so we can't use
                    // SpanContextPropagator (which also injects Datadog headers with hyphens).
                    // Instead, inject W3C traceparent/tracestate directly — this is the standard
                    // for CloudEvents distributed tracing. Pre-populating these keys also prevents
                    // the Azure SDK from overwriting with its own Activity-based context.
                    if (evt.TryDuckCast<ICloudEvent>(out var cloudEvent)
                        && cloudEvent.ExtensionAttributes is { } attrs)
                    {
                        InjectW3CContext(attrs, scope);
                    }
                }
            }

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventGrid);

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
    /// Injects W3C traceparent and tracestate into CloudEvent ExtensionAttributes.
    /// Uses the W3C propagator directly because CloudEvent extension attribute names
    /// only allow lowercase letters and digits — Datadog-format headers (x-datadog-*)
    /// would throw ArgumentException.
    /// </summary>
    private static void InjectW3CContext(IDictionary<string, object> extensionAttributes, Scope scope)
    {
        if (scope.Span.Context is not { } spanContext)
        {
            return;
        }

        try
        {
            var traceparent = W3CTraceContextPropagator.CreateTraceParentHeader(spanContext);
            extensionAttributes[W3CTraceContextPropagator.TraceParentHeaderName] = traceparent;

            var tracestate = W3CTraceContextPropagator.CreateTraceStateHeader(spanContext);
            if (!StringUtil.IsNullOrEmpty(tracestate))
            {
                extensionAttributes[W3CTraceContextPropagator.TraceStateHeaderName] = tracestate;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to inject W3C trace context into CloudEvent ExtensionAttributes");
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
