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
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;

/// <summary>
/// System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]] Azure.Messaging.ServiceBus.ServiceBusReceiver::ReceiveMessagesAsync(System.Int32,System.Nullable`1[System.TimeSpan],System.Boolean,System.Threading.CancellationToken) calltarget instrumentation
///
/// This integration has special handling for Azure Functions with ServiceBus triggers:
/// - When running in an Azure Functions ServiceBus trigger context, it modifies the existing Azure Functions span
///   instead of creating a new servicebus.receive span to avoid duplicate spans
/// - When running outside of Azure Functions, it creates a normal servicebus.receive span
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusReceiver",
    MethodName = "ReceiveMessagesAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]]",
    ParameterTypeNames = [ClrNames.Int32, "System.Nullable`1[System.TimeSpan]", ClrNames.Bool, ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ServiceBusReceiverReceiveMessagesAsyncIntegration
{
    private const string OperationName = "servicebus.receive";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusReceiverReceiveMessagesAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int maxMessages, TimeSpan? maxWaitTime, bool isProcessor, CancellationToken cancellationToken)
        where TTarget : IServiceBusReceiver, IDuckType
    {
        Log.Information("ServiceBusReceiverReceiveMessagesAsyncIntegration.OnMethodBegin called");
        var tracer = Tracer.Instance;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
        {
            return CallTargetState.GetDefault();
        }

        var startTime = DateTimeOffset.UtcNow;
        return new CallTargetState(null, new ReceiveMessagesState(instance, startTime));
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Log.Information("ServiceBusReceiverReceiveMessagesAsyncIntegration.OnAsyncMethodEnd called");

        var tracer = Tracer.Instance;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus) ||
            !(state.State is ReceiveMessagesState stateData))
        {
            return returnValue;
        }

        var receiverInstance = stateData.Instance;
        var startTime = stateData.StartTime;
        var messagesList = returnValue as System.Collections.IList;
        var messageCount = messagesList?.Count ?? 0;

        if (exception == null && messageCount == 0)
        {
            return returnValue;
        }

        var contextInfo = ExtractContextAndMessageId(tracer, messagesList);
        var parentContext = contextInfo.ParentContext;
        var spanLinks = contextInfo.SpanLinks;
        var messageId = contextInfo.MessageId;

        // Check if we're in an Azure Functions ServiceBus context only when we need to create/modify a span
        var azureFunctionsSpan = GetAzureFunctionsServiceBusSpan(tracer);

        if (azureFunctionsSpan != null)
        {
            ModifyAzureFunctionsSpan(azureFunctionsSpan, receiverInstance, messageId, exception);
        }
        else
        {
            CreateAndConfigureSpan(tracer, parentContext, spanLinks, receiverInstance, startTime, messageId, exception);
        }

        return returnValue;
    }

    private static ContextExtractionResult ExtractContextAndMessageId(Tracer tracer, System.Collections.IList? messagesList)
    {
        if (messagesList == null || messagesList.Count == 0)
        {
            return new ContextExtractionResult(null, null, null);
        }

        // Single message case: extract both context and message ID
        if (messagesList.Count == 1)
        {
            var message = messagesList[0];
            if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true)
            {
                var context = ExtractContextFromMessage(tracer, serviceBusMessage);
                var messageId = string.IsNullOrEmpty(serviceBusMessage.MessageId) ? null : serviceBusMessage.MessageId;

                return new ContextExtractionResult(context, null, messageId);
            }

            return new ContextExtractionResult(null, null, null);
        }

        // Multiple messages case: collect all contexts
        var contexts = new List<SpanContext>();

        for (int i = 0; i < messagesList.Count; i++)
        {
            var message = messagesList[i];
            if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true)
            {
                var context = ExtractContextFromMessage(tracer, serviceBusMessage);
                if (context != null)
                {
                    contexts.Add(context);
                }
            }
        }

        if (contexts.Count == 0)
        {
            return new ContextExtractionResult(null, null, null);
        }

        // Check if all contexts are the same
        var firstContext = contexts[0];
        bool allSame = contexts.All(c => c.TraceId128 == firstContext.TraceId128 && c.SpanId == firstContext.SpanId);

        if (allSame)
        {
            return new ContextExtractionResult(firstContext, null, null); // Use as parent
        }
        else
        {
            var spanLinks = contexts.Select(c => new SpanLink(c)).ToArray();
            return new ContextExtractionResult(null, spanLinks, null); // Use span links instead of parent
        }
    }

    private static SpanContext? ExtractContextFromMessage(Tracer tracer, IServiceBusReceivedMessage message)
    {
        if (message.ApplicationProperties == null)
        {
            return null;
        }

        try
        {
            var headerAdapter = new ServiceBusHeadersCollectionAdapter(message.ApplicationProperties);
            var extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headerAdapter);
            return extractedContext.SpanContext;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting context from ServiceBus message");
            return null;
        }
    }

    private static void CreateAndConfigureSpan(
        Tracer tracer,
        SpanContext? parentContext,
        SpanLink[]? spanLinks,
        IServiceBusReceiver receiverInstance,
        DateTimeOffset startTime,
        string? messageId,
        Exception? exception)
    {
        var scope = tracer.StartActiveInternal(OperationName, parent: parentContext, links: spanLinks, startTime: startTime);
        var span = scope.Span;

        try
        {
            span.Type = SpanTypes.Queue;
            span.SetTag(Tags.SpanKind, SpanKinds.Consumer);

            var entityPath = receiverInstance.EntityPath ?? "unknown";
            span.ResourceName = entityPath;

            span.SetTag(Tags.MessagingDestinationName, entityPath);
            span.SetTag(Tags.MessagingOperation, "receive");
            span.SetTag(Tags.MessagingSystem, "servicebus");

            if (messageId != null)
            {
                span.SetTag(Tags.MessagingMessageId, messageId);
            }

            if (exception != null)
            {
                span.SetException(exception);
            }
        }
        finally
        {
            scope.Dispose();
        }
    }

    private static Span? GetAzureFunctionsServiceBusSpan(Tracer tracer)
    {
        var rootSpan = tracer.InternalActiveScope?.Root?.Span;
        return rootSpan?.Type == SpanTypes.Serverless &&
               rootSpan.ResourceName?.StartsWith("ServiceBus ", StringComparison.OrdinalIgnoreCase) == true
               ? rootSpan
               : null;
    }

    private static void ModifyAzureFunctionsSpan(Span azureFunctionsSpan, IServiceBusReceiver receiverInstance, string? messageId, Exception? exception)
    {
        var entityPath = receiverInstance.EntityPath ?? "unknown";

        // Azure Functions has already extracted and set the parent context from the ServiceBus message
        // We only need to add the messaging-specific tags here
        azureFunctionsSpan.SetTag(Tags.MessagingDestinationName, entityPath);
        azureFunctionsSpan.SetTag(Tags.MessagingOperation, "receive");
        azureFunctionsSpan.SetTag(Tags.MessagingSystem, "servicebus");
        azureFunctionsSpan.SetTag(Tags.SpanKind, SpanKinds.Consumer);

        if (messageId != null)
        {
            azureFunctionsSpan.SetTag(Tags.MessagingMessageId, messageId);
        }

        // We don't modify span.Type or span.ResourceName as those are already
        // set appropriately by AzureFunctionsCommon (Type = "serverless", ResourceName = "ServiceBus {functionName}")

        if (exception != null)
        {
            azureFunctionsSpan.SetException(exception);
        }
    }

    private readonly struct ReceiveMessagesState
    {
        public readonly IServiceBusReceiver Instance;
        public readonly DateTimeOffset StartTime;

        public ReceiveMessagesState(IServiceBusReceiver instance, DateTimeOffset startTime)
        {
            Instance = instance;
            StartTime = startTime;
        }
    }

    private readonly struct ContextExtractionResult
    {
        public readonly SpanContext? ParentContext;
        public readonly SpanLink[]? SpanLinks;
        public readonly string? MessageId;

        public ContextExtractionResult(SpanContext? parentContext, SpanLink[]? spanLinks, string? messageId)
        {
            ParentContext = parentContext;
            SpanLinks = spanLinks;
            MessageId = messageId;
        }
    }
}
