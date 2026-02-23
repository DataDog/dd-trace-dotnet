// <copyright file="IbmMqHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;

internal static class IbmMqHelper
{
    private const string QueueUriPrefix = "queue://";
    private const string TopicUriPrefix = "topic://";

    /// <summary>
    /// Sanitizes an IBM MQ queue name by removing URI scheme prefixes.
    /// Converts names like "queue://my_queue" to "my_queue".
    /// </summary>
    internal static string SanitizeQueueName(string? queueName)
    {
        if (StringUtil.IsNullOrEmpty(queueName))
        {
            return string.Empty;
        }

        // Remove queue:// or topic:// URI prefix if present
        if (queueName.StartsWith(QueueUriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return queueName.Substring(QueueUriPrefix.Length);
        }

        if (queueName.StartsWith(TopicUriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return queueName.Substring(TopicUriPrefix.Length);
        }

        return queueName;
    }

    internal static IbmMqHeadersAdapterNoop GetHeadersAdapter(IMqMessage message)
    {
        // we temporarily switch to noop adapter, since
        // multiple customers reported issues with context propagation.
        // The goal is to allow context injection only when we have a way of configuring
        // this on per-instrumentation basis.
        return new IbmMqHeadersAdapterNoop();
    }

    internal static Scope? CreateProducerScope(Tracer tracer, IMqQueue queue, IMqMessage message)
    {
        Scope? scope = null;

        try
        {
            var settings = tracer.CurrentTraceSettings;
            if (!settings.Settings.IsIntegrationEnabled(IntegrationId.IbmMq))
            {
                return null;
            }

            var operationName = settings.Schema.Messaging.GetOutboundOperationName(MessagingSchema.OperationType.IbmMq);
            var serviceName = settings.Schema.Messaging.GetServiceName(MessagingSchema.ServiceType.IbmMq);
            var tags = settings.Schema.Messaging.CreateIbmMqTags(SpanKinds.Consumer);
            var queueName = SanitizeQueueName(queue.Name);
            tags.TopicName = queueName;

            scope = tracer.StartActiveInternal(
                operationName,
                serviceName: serviceName,
                finishOnClose: true);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.IbmMq);

            var resourceName = $"Produce Topic {(string.IsNullOrEmpty(queueName) ? "ibmmq" : queueName)}";

            var span = scope.Span;
            span.Type = SpanTypes.Queue;
            span.ResourceName = resourceName;
            span.SetTag(Tags.SpanKind, SpanKinds.Producer);

            var context = new PropagationContext(span.Context, Baggage.Current);
            tracer.TracerManager.SpanContextPropagator.Inject(context, GetHeadersAdapter(message));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating or populating scope.");
        }

        return scope;
    }

    internal static Scope? CreateConsumerScope(
        Tracer tracer,
        DateTimeOffset? spanStartTime,
        IMqQueue queue,
        IMqMessage message)
    {
        Scope? scope = null;

        try
        {
            var settings = tracer.CurrentTraceSettings;
            if (!settings.Settings.IsIntegrationEnabled(IntegrationId.IbmMq))
            {
                return null;
            }

            var parent = tracer.ActiveScope?.Span;
            var operationName = settings.Schema.Messaging.GetInboundOperationName(MessagingSchema.OperationType.IbmMq);
            if (parent is not null &&
                parent.OperationName == operationName &&
                parent.GetTag(Tags.InstrumentationName) != null)
            {
                return null;
            }

            PropagationContext extractedContext = default;

            try
            {
                var headers = GetHeadersAdapter(message);
                extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headers).MergeBaggageInto(Baggage.Current);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated headers from IbmMq message");
            }

            var serviceName = settings.Schema.Messaging.GetServiceName(MessagingSchema.ServiceType.IbmMq);
            var tags = settings.Schema.Messaging.CreateIbmMqTags(SpanKinds.Producer);
            var queueName = SanitizeQueueName(queue.Name);
            tags.TopicName = queueName;
            scope = tracer.StartActiveInternal(
                operationName,
                tags: tags,
                parent: extractedContext.SpanContext,
                serviceName: serviceName,
                finishOnClose: true,
                startTime: spanStartTime);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.IbmMq);
            var resourceName = $"Consume Topic {(string.IsNullOrEmpty(queueName) ? "ibmmq" : queueName)}";

            var span = scope.Span;
            span.Type = SpanTypes.Queue;
            span.ResourceName = resourceName;
            span.SetTag(Tags.SpanKind, SpanKinds.Consumer);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating or populating scope.");
        }

        return scope;
    }
}
