// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;

/// <summary>
/// System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]] Azure.Messaging.ServiceBus.ServiceBusReceiver::ReceiveMessagesAsync(System.Int32,System.Nullable`1[System.TimeSpan],System.Boolean,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusReceiver",
    MethodName = "ReceiveMessagesAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]]",
    ParameterTypeNames = [ClrNames.Int32, "System.Nullable`1[System.TimeSpan]", ClrNames.Bool, ClrNames.CancellationToken],
    MinimumVersion = "7.14.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ServiceBusReceiverReceiveMessagesAsyncIntegration
{
    private const string OperationName = "azure_servicebus.receive";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusReceiverReceiveMessagesAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int maxMessages, TimeSpan? maxWaitTime, bool isProcessor, CancellationToken cancellationToken)
        where TTarget : IServiceBusReceiver, IDuckType
    {
        return CallTargetState.GetDefault();
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
        where TTarget : IServiceBusReceiver, IDuckType
    {
        var tracer = Tracer.Instance;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus, false))
        {
            return returnValue;
        }

        var messagesList = returnValue as System.Collections.IList;
        var messageCount = messagesList?.Count ?? 0;

        // Don't create spans when there's an exception or no messages
        if (exception != null || messageCount == 0)
        {
            return returnValue;
        }

        var extractionResult = ExtractContextsFromMessages(tracer, messagesList);
        var scope = CreateAndConfigureSpan(tracer, extractionResult.ParentContext, extractionResult.SpanLinks, instance, messagesList);

        // Re-inject the new span context into all messages so Azure Functions will use it as parent
        if (scope != null && messagesList != null && messageCount > 0)
        {
            ReinjectContextIntoMessages(tracer, scope, messagesList);
        }

        if (scope != null)
        {
            scope.Dispose();
        }

        return returnValue;
    }

    private static ContextExtractionResult ExtractContextsFromMessages(Tracer tracer, System.Collections.IList? messagesList)
    {
        if (messagesList == null || messagesList.Count == 0)
        {
            return new ContextExtractionResult(null, null);
        }

        var extractedContexts = new List<SpanContext>();

        try
        {
            foreach (var message in messagesList)
            {
                if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true &&
                    serviceBusMessage.ApplicationProperties != null)
                {
                    var headerAdapter = new ServiceBusHeadersCollectionAdapter(serviceBusMessage.ApplicationProperties);
                    var extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headerAdapter);
                    if (extractedContext.SpanContext != null)
                    {
                        extractedContexts.Add(extractedContext.SpanContext);
                    }
                }
            }

            if (extractedContexts.Count == 0)
            {
                return new ContextExtractionResult(null, null);
            }

            // Check if all contexts are the same
            var firstContext = extractedContexts[0];
            var comparer = new SpanContextComparer();
            var allSame = extractedContexts.All(ctx => comparer.Equals(ctx, firstContext));

            if (allSame)
            {
                // All messages have the same context, use it as parent
                return new ContextExtractionResult(firstContext, null);
            }
            else
            {
                // Heterogeneous contexts, create span links to all of them
                var spanLinks = extractedContexts
                    .Distinct(new SpanContextComparer())
                    .Select(ctx => new SpanLink(ctx))
                    .ToList();
                return new ContextExtractionResult(null, spanLinks);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ServiceBusReceiver: Error extracting contexts from ServiceBus messages");
        }

        return new ContextExtractionResult(null, null);
    }

    private static Scope? CreateAndConfigureSpan<TTarget>(
        Tracer tracer,
        SpanContext? parentContext,
        IEnumerable<SpanLink>? spanLinks,
        TTarget receiverInstance,
        System.Collections.IList? messagesList)
        where TTarget : IServiceBusReceiver
    {
        var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAzureServiceBusTags(SpanKinds.Consumer);

        var entityPath = receiverInstance.EntityPath ?? "unknown";
        tags.MessagingDestinationName = entityPath;
        tags.MessagingOperation = "receive";
        tags.MessagingSystem = "servicebus";
        tags.InstrumentationName = "AzureServiceBus";

        string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName("azureservicebus");
        var scope = tracer.StartActiveInternal(
            OperationName,
            parent: parentContext,
            links: spanLinks,
            tags: tags,
            serviceName: serviceName);
        var span = scope.Span;

        span.Type = SpanTypes.Queue;
        span.ResourceName = entityPath;

        // Set MessagingMessageId if single message received
        if (messagesList?.Count == 1)
        {
            var message = messagesList[0];
            if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true)
            {
                var messageId = serviceBusMessage.MessageId;
                if (!string.IsNullOrEmpty(messageId))
                {
                    span.SetTag(Tags.MessagingMessageId, messageId);
                }
            }
        }

        return scope;
    }

    private static void ReinjectContextIntoMessages(Tracer tracer, Scope scope, System.Collections.IList messagesList)
    {
        try
        {
            var context = new Propagators.PropagationContext(scope.Span.Context, Baggage.Current);

            foreach (var message in messagesList)
            {
                if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true)
                {
                    var amqpMessage = serviceBusMessage.AmqpMessage;
                    if (amqpMessage?.ApplicationProperties != null)
                    {
                        var headerAdapter = new ServiceBusHeadersCollectionAdapter(amqpMessage.ApplicationProperties);
                        tracer.TracerManager.SpanContextPropagator.Inject(context, headerAdapter);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ServiceBusReceiver: Error re-injecting context into ServiceBus messages");
        }
    }

    private readonly struct ContextExtractionResult
    {
        public readonly SpanContext? ParentContext;
        public readonly IEnumerable<SpanLink>? SpanLinks;

        public ContextExtractionResult(SpanContext? parentContext, IEnumerable<SpanLink>? spanLinks)
        {
            ParentContext = parentContext;
            SpanLinks = spanLinks;
        }
    }

    private class SpanContextComparer : IEqualityComparer<SpanContext>
    {
        public bool Equals(SpanContext? x, SpanContext? y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }

            return x.TraceId128 == y.TraceId128 && x.SpanId == y.SpanId;
        }

        public int GetHashCode(SpanContext obj)
        {
            return HashCode.Combine(obj.TraceId128, obj.SpanId);
        }
    }
}
