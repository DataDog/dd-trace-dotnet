// <copyright file="IbmMqHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;

internal static class IbmMqHelper
{
    private const string MessagingType = "ibmmq";

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

            scope = tracer.StartActiveInternal(
                operationName,
                serviceName: serviceName,
                finishOnClose: true);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.IbmMq);

            var resourceName = $"Produce Topic {queue.Name}";

            var span = scope.Span;
            span.Type = SpanTypes.Queue;
            span.ResourceName = resourceName;
            span.SetTag(Tags.SpanKind, SpanKinds.Producer);

            var adapter = new IbmMqHeadersAdapter(message);
            SpanContextPropagator.Instance.Inject(span.Context, adapter);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating or populating scope.");
        }

        return scope;
    }

    internal static Scope? CreateConsumerScope(
        Tracer tracer,
        CallTargetState state,
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
            PathwayContext? pathwayContext = null;

            var adapter = new IbmMqHeadersAdapter(message);
            try
            {
                propagatedContext = SpanContextPropagator.Instance.Extract(adapter);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated headers from IbmMq message");
            }

            var dataStreams = tracer.TracerManager.DataStreamsManager;
            if (dataStreams.IsEnabled)
            {
                try
                {
                    pathwayContext = dataStreams.ExtractPathwayContextAsBase64String(adapter);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting PathwayContext from IbmMq message");
                }
            }

            var serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName(MessagingType);
            scope = tracer.StartActiveInternal(
                operationName,
                parent: propagatedContext,
                serviceName: serviceName,
                finishOnClose: true,
                startTime: state.StartTime);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.IbmMq);

            var resourceName = $"Consume Topic {(string.IsNullOrEmpty(queue.Name) ? "ibmmq" : queue.Name)}";

            var span = scope.Span;
            span.Type = SpanTypes.Queue;
            span.ResourceName = resourceName;
            span.SetTag(Tags.SpanKind, SpanKinds.Consumer);

            if (dataStreams.IsEnabled)
            {
                span.Context.MergePathwayContext(pathwayContext);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating or populating scope.");
        }

        return scope;
    }
}
