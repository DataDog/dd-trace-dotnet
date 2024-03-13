// <copyright file="IbmMqHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;

internal static class IbmMqHelper
{
    private const string MessagingType = "ibmmq";
    private static readonly IbmMqHeadersAdapterNoop NoopAdapter = new();

    internal static IHeadersCollection GetHeadersAdapter(IMqMessage message)
    {
        // we temporary switch to noop adapter, since
        // multiple customers reported issues with context propagation.
        // The goal is to allow context injection only when we have a way of configuring
        // this on per-instrumentation basis.
        return NoopAdapter;
    }

    internal static Scope? CreateProducerScope(Tracer tracer, IMqQueue queue, IMqMessage message)
    {
        Scope? scope = null;

        try
        {
            var settings = tracer.Settings;
            if (!settings.IsIntegrationEnabled(IntegrationId.IbmMq))
            {
                return null;
            }

            var operationName = tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundOperationName(MessagingType);
            var serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName(MessagingType);
            var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateIbmMqTags(SpanKinds.Consumer);
            tags.TopicName = queue.Name;

            scope = tracer.StartActiveInternal(
                operationName,
                serviceName: serviceName,
                finishOnClose: true);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.IbmMq);

            var resourceName = $"Produce Topic {(string.IsNullOrEmpty(queue.Name) ? "ibmmq" : queue.Name)}";

            var span = scope.Span;
            span.Type = SpanTypes.Queue;
            span.ResourceName = resourceName;
            span.SetTag(Tags.SpanKind, SpanKinds.Producer);

            SpanContextPropagator.Instance.Inject(span.Context, GetHeadersAdapter(message));
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
            var settings = tracer.Settings;
            if (!settings.IsIntegrationEnabled(IntegrationId.IbmMq))
            {
                return null;
            }

            var parent = tracer.ActiveScope?.Span;
            var operationName = tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(MessagingType);
            if (parent is not null &&
                parent.OperationName == operationName &&
                parent.GetTag(Tags.InstrumentationName) != null)
            {
                return null;
            }

            SpanContext? propagatedContext = null;

            try
            {
                propagatedContext = SpanContextPropagator.Instance.Extract(GetHeadersAdapter(message));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated headers from IbmMq message");
            }

            var serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName(MessagingType);
            var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateIbmMqTags(SpanKinds.Producer);
            tags.TopicName = queue.Name;
            scope = tracer.StartActiveInternal(
                operationName,
                tags: tags,
                parent: propagatedContext,
                serviceName: serviceName,
                finishOnClose: true,
                startTime: spanStartTime);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.IbmMq);
            var resourceName = $"Consume Topic {(string.IsNullOrEmpty(queue.Name) ? "ibmmq" : queue.Name)}";

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
