// <copyright file="EventGridCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

    internal static CallTargetState CreateProducerSpan<TTarget, TEvents>(TTarget instance, ref TEvents events, bool injectContext, string? destinationNameOverride = null)
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventGrid))
        {
            return CallTargetState.GetDefault();
        }

        // The Functions host loads the Event Grid extension in a separate AssemblyLoadContext. Accessing
        // this private field through a duck-type proxy can fail with MissingFieldException even though the
        // field exists, so retrieve the field value from the concrete target type before duck casting it.
        IRequestUriBuilder? uriBuilder = null;
        if ((object?)instance is { } target)
        {
            uriBuilder = UriBuilderFieldCache<TTarget>.Field?.GetValue(target)?.DuckCast<IRequestUriBuilder>();
        }

        var host = uriBuilder?.Host;
        var port = uriBuilder?.Port ?? -1;
        var destinationName = destinationNameOverride ?? GetTopicFromHost(host);
        return CreateProducerSpan(tracer, destinationName, host, port, ref events, injectContext);
    }

    internal static CallTargetState CreateNamespaceProducerSpanForEvent<TTarget>(TTarget instance, object? cloudEvent)
        where TTarget : IEventGridSenderClient
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventGrid))
        {
            return CallTargetState.GetDefault();
        }

        var endpoint = instance.Endpoint;
        return CreateProducerSpan(tracer, instance.TopicName, endpoint?.Host, endpoint?.Port ?? -1, events: null, cloudEvent);
    }

    internal static CallTargetState CreateNamespaceProducerSpanForEvents<TTarget, TEvents>(TTarget instance, ref TEvents cloudEvents)
        where TTarget : IEventGridSenderClient
    {
        var tracer = Tracer.Instance;
        if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureEventGrid))
        {
            return CallTargetState.GetDefault();
        }

        var endpoint = instance.Endpoint;
        return CreateProducerSpan(tracer, instance.TopicName, endpoint?.Host, endpoint?.Port ?? -1, ref cloudEvents, injectContext: true);
    }

    private static CallTargetState CreateProducerSpan<TEvents>(Tracer tracer, string? destinationName, string? host, int port, ref TEvents events, bool injectContext)
    {
        var enumerable = events as IEnumerable;
        var state = CreateProducerSpan(tracer, destinationName, host, port, enumerable, singleEvent: null);
        if (state.Scope is not { } scope || enumerable is null)
        {
            return state;
        }

        try
        {
            var observer = new EventGridEnumerableObserver(scope, enumerable is ICollection collection ? collection.Count : null, injectContext);
            events = EventGridObservingEnumerable.Wrap(events, observer);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error wrapping Azure Event Grid events for context injection");
        }

        return state;
    }

    private static CallTargetState CreateProducerSpan(Tracer tracer, string? destinationName, string? host, int port, IEnumerable? events, object? singleEvent)
    {
        Scope? scope = null;

        try
        {
            var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureEventGridTags(SpanKinds.Producer);
            tags.MessagingOperation = "send";

            tags.MessagingDestinationName = destinationName;
            tags.NetworkDestinationName = host;

            if (port is not -1)
            {
                tags.NetworkDestinationPort = port.ToString();
            }

            var messageCount = singleEvent is not null ? 1 : events is ICollection collection ? collection.Count : 0;
            if (messageCount > 1)
            {
                tags.MessagingBatchMessageCount = messageCount.ToString();
            }

            var (serviceName, serviceNameSource) = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.AzureEventGrid);
            scope = tracer.StartActiveInternal("azure_eventgrid.send", tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
            var span = scope.Span;

            span.Type = SpanTypes.Queue;
            span.ResourceName = destinationName;

            if (singleEvent is not null)
            {
                ProcessEvent(singleEvent, messageCount, span, scope);
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

    private static void ProcessEvent(object evt, int messageCount, Span span, Scope scope)
    {
        if (messageCount == 1)
        {
            SetMessageId(evt, span);
        }

        // Inject W3C trace context and baggage into CloudEvent ExtensionAttributes.
        // CloudEvent extension attribute names only allow [a-z0-9], so we can't use
        // SpanContextPropagator (which also injects Datadog headers with hyphens).
        // Instead, inject W3C traceparent/tracestate/baggage directly — this is the standard
        // for CloudEvents distributed tracing. Pre-populating these keys also prevents
        // the Azure SDK from overwriting with its own Activity-based context.
        InjectContext(evt, scope);
    }

    private static void InjectContext(object evt, Scope scope)
    {
        if (evt.TryDuckCast<ICloudEvent>(out var cloudEvent)
            && cloudEvent.ExtensionAttributes is { } attrs)
        {
            InjectW3CContext(attrs, scope, Tracer.Instance.Settings.PropagationStyleInject);
        }
    }

    private static void SetMessageId(object evt, Span span)
    {
        if (evt.DuckCast<IEventGridEventId>() is { Id: { } id } && id.Length > 0)
        {
            span.SetTag(Tags.MessagingMessageId, id);
        }
    }

    /// <summary>
    /// Injects the configured W3C traceparent, tracestate, and baggage into CloudEvent ExtensionAttributes.
    /// Uses the W3C propagator directly because CloudEvent extension attribute names
    /// only allow lowercase letters and digits — Datadog-format headers (x-datadog-*)
    /// would throw ArgumentException.
    /// </summary>
    internal static void InjectW3CContext(IDictionary<string, object> extensionAttributes, Scope scope, string[] propagationStyles)
    {
        if (scope.Span.Context is not { } spanContext)
        {
            return;
        }

        try
        {
            var context = new PropagationContext(spanContext, Baggage.Current);
            var carrier = default(Shared.AzureMessagingCommon.DictionaryContextPropagation);

            if (IsPropagationStyleEnabled(propagationStyles, ContextPropagationHeaderStyle.W3CTraceContext) ||
                IsPropagationStyleEnabled(propagationStyles, ContextPropagationHeaderStyle.Deprecated.W3CTraceContext))
            {
                // The propagator does not clear an existing optional value when the current
                // context has none, so remove it before injecting a reused CloudEvent.
                extensionAttributes.Remove(W3CTraceContextPropagator.TraceStateHeaderName);
                W3CTraceContextPropagator.Instance.Inject(context, extensionAttributes, carrier);
            }

            if (IsPropagationStyleEnabled(propagationStyles, ContextPropagationHeaderStyle.W3CBaggage))
            {
                extensionAttributes.Remove(W3CBaggagePropagator.BaggageHeaderName);
                W3CBaggagePropagator.Instance.Inject(context, extensionAttributes, carrier);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to inject W3C trace context into CloudEvent ExtensionAttributes");
        }
    }

    private static bool IsPropagationStyleEnabled(string[] propagationStyles, string expectedStyle)
    {
        foreach (var propagationStyle in propagationStyles)
        {
            if (string.Equals(propagationStyle, expectedStyle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private static class UriBuilderFieldCache<TTarget>
    {
        public static readonly FieldInfo? Field = typeof(TTarget).GetField("_uriBuilder", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private sealed class EventGridEnumerableObserver : EventGridObservingEnumerable.IObserver
    {
        private readonly Scope _scope;
        private readonly int? _knownCount;
        private readonly bool _injectContext;

        public EventGridEnumerableObserver(Scope scope, int? knownCount, bool injectContext)
        {
            _scope = scope;
            _knownCount = knownCount;
            _injectContext = injectContext;
        }

        public void OnItem(object? item)
        {
            try
            {
                if (item is not null)
                {
                    if (_knownCount == 1)
                    {
                        SetMessageId(item, _scope.Span);
                    }

                    if (_injectContext)
                    {
                        InjectContext(item, _scope);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error processing an Azure Event Grid event");
            }
        }

        public void OnCompleted(int count, object? firstItem)
        {
            try
            {
                if (!_knownCount.HasValue && count > 1)
                {
                    _scope.Span.SetTag(Tags.MessagingBatchMessageCount, count.ToString());
                }

                if (!_knownCount.HasValue && count == 1 && firstItem is not null)
                {
                    SetMessageId(firstItem, _scope.Span);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error finalizing Azure Event Grid event processing");
            }
        }
    }
}
