// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            Log.Debug("ServiceBusReceiver: Integration not enabled or invalid state");
            return returnValue;
        }

        var receiverInstance = stateData.Instance;
        var startTime = stateData.StartTime;
        var messagesList = returnValue as System.Collections.IList;
        var messageCount = messagesList?.Count ?? 0;

        Log.Information("ServiceBusReceiver: Processing {MessageCount} messages", messageCount.ToString());

        if (exception == null && messageCount == 0)
        {
            Log.Debug("ServiceBusReceiver: No messages to process");
            return returnValue;
        }

        var parentContext = ExtractParentContextFromFirstMessage(tracer, messagesList);
        var scope = CreateAndConfigureSpan(tracer, parentContext, receiverInstance, startTime, exception);

        // Re-inject the new span context into all messages so Azure Functions will use it as parent
        if (scope != null && messagesList != null && messageCount > 0)
        {
            Log.Information("ServiceBusReceiver: Attempting to re-inject context into {MessageCount} messages", messageCount.ToString());
            ReinjectContextIntoMessages(tracer, scope, messagesList);
        }
        else
        {
            Log.Warning(
                "ServiceBusReceiver: Cannot re-inject - scope={ScopeNull}, messagesList={ListNull}, messageCount={Count}",
                (scope == null).ToString(),
                (messagesList == null).ToString(),
                messageCount.ToString());
        }

        return returnValue;
    }

    private static SpanContext? ExtractParentContextFromFirstMessage(Tracer tracer, System.Collections.IList? messagesList)
    {
        if (messagesList == null || messagesList.Count == 0)
        {
            Log.Debug("ServiceBusReceiver: No messages to extract context from");
            return null;
        }

        try
        {
            var firstMessage = messagesList[0];
            Log.Debug(
                "ServiceBusReceiver: First message is null? {IsNull}, Type: {Type}",
                (firstMessage == null).ToString(),
                firstMessage?.GetType().FullName ?? "null");
            if (firstMessage?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true)
            {
                Log.Debug(
                    "ServiceBusReceiver: Duck cast successful, ApplicationProperties null? {IsNull}",
                    (serviceBusMessage.ApplicationProperties == null).ToString());

                if (serviceBusMessage.ApplicationProperties != null)
                {
                    Log.Information(
                        "ServiceBusReceiver: ApplicationProperties count: {Count}",
                        serviceBusMessage.ApplicationProperties.Count.ToString());

                    // Log all properties for debugging
                    foreach (var kvp in serviceBusMessage.ApplicationProperties)
                    {
                        Log.Debug("ServiceBusReceiver: Property [{Key}] = {Value}", kvp.Key, kvp.Value);
                    }

                    var headerAdapter = new ServiceBusHeadersCollectionAdapter(serviceBusMessage.ApplicationProperties);
                    var extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headerAdapter);
                    var parentContext = extractedContext.SpanContext;

                    if (parentContext != null)
                    {
                        Log.Information(
                            "ServiceBusReceiver: Successfully extracted parent context - TraceId: {TraceId}, SpanId: {SpanId}",
                            parentContext.TraceId128,
                            parentContext.SpanId);
                    }
                    else
                    {
                        Log.Warning("ServiceBusReceiver: Extract returned null SpanContext");
                    }

                    return parentContext;
                }
            }
            else
            {
                Log.Warning(
                    "ServiceBusReceiver: Duck cast failed for message type {Type}",
                    firstMessage?.GetType().FullName ?? "null");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ServiceBusReceiver: Error extracting parent context from ServiceBus message");
        }

        Log.Warning("ServiceBusReceiver: Failed to extract parent context from first message");
        return null;
    }

    private static Scope? CreateAndConfigureSpan(
        Tracer tracer,
        SpanContext? parentContext,
        IServiceBusReceiver receiverInstance,
        DateTimeOffset startTime,
        Exception? exception)
    {
        Log.Information(
            "ServiceBusReceiver: Creating span with parent context - TraceId: {TraceId}, SpanId: {SpanId}",
            parentContext?.TraceId128.ToString() ?? "null",
            parentContext?.SpanId.ToString() ?? "0");

        var scope = tracer.StartActiveInternal(OperationName, parent: parentContext, startTime: startTime);
        var span = scope.Span;

        Log.Information(
            "ServiceBusReceiver: Created span - TraceId: {TraceId}, SpanId: {SpanId}, ParentId: {ParentId}",
            span.Context.TraceId128.ToString(),
            span.Context.SpanId.ToString(),
            (span.Context.ParentId ?? 0).ToString());

        try
        {
            span.Type = SpanTypes.Queue;
            span.SetTag(Tags.SpanKind, SpanKinds.Consumer);

            var entityPath = receiverInstance.EntityPath ?? "unknown";
            span.ResourceName = entityPath;

            span.SetTag(Tags.MessagingDestinationName, entityPath);
            span.SetTag(Tags.MessagingOperation, "receive");
            span.SetTag(Tags.MessagingSystem, "servicebus");

            if (exception != null)
            {
                span.SetException(exception);
            }
        }
        finally
        {
            scope.Dispose();
        }

        return scope;
    }

    private static void ReinjectContextIntoMessages(Tracer tracer, Scope scope, System.Collections.IList messagesList)
    {
        try
        {
            var context = new Propagators.PropagationContext(scope.Span.Context, Baggage.Current);
            var injectedCount = 0;

            foreach (var message in messagesList)
            {
                Log.Debug(
                    "ServiceBusReceiver: Processing message for re-injection, null? {IsNull}, Type: {Type}",
                    (message == null).ToString(),
                    message?.GetType().FullName ?? "null");

                if (message?.TryDuckCast<IServiceBusReceivedMessage>(out var serviceBusMessage) == true)
                {
                    if (serviceBusMessage.ApplicationProperties != null)
                    {
                        Log.Debug(
                            "ServiceBusReceiver: Before re-injection, ApplicationProperties count: {Count}",
                            serviceBusMessage.ApplicationProperties.Count.ToString());

                        // Log properties before injection
                        foreach (var kvp in serviceBusMessage.ApplicationProperties)
                        {
                            if (kvp.Key.StartsWith("x-datadog") || kvp.Key.StartsWith("traceparent"))
                            {
                                Log.Debug("ServiceBusReceiver: Before - Property [{Key}] = {Value}", kvp.Key, kvp.Value);
                            }
                        }

                        var headerAdapter = new ServiceBusHeadersCollectionAdapter(serviceBusMessage.ApplicationProperties);
                        tracer.TracerManager.SpanContextPropagator.Inject(context, headerAdapter);
                        injectedCount++;

                        // Log properties after injection
                        foreach (var kvp in serviceBusMessage.ApplicationProperties)
                        {
                            if (kvp.Key.StartsWith("x-datadog") || kvp.Key.StartsWith("traceparent"))
                            {
                                Log.Debug("ServiceBusReceiver: After - Property [{Key}] = {Value}", kvp.Key, kvp.Value);
                            }
                        }

                        Log.Information(
                            "ServiceBusReceiver: Re-injected context into message {Index} - TraceId: {TraceId}, SpanId: {SpanId}",
                            injectedCount,
                            scope.Span.Context.TraceId128,
                            scope.Span.Context.SpanId);
                    }
                    else
                    {
                        Log.Warning("ServiceBusReceiver: Message has null ApplicationProperties, cannot re-inject");
                    }
                }
                else
                {
                    Log.Warning("ServiceBusReceiver: Failed to duck cast message for re-injection");
                }
            }

            Log.Information(
                "ServiceBusReceiver: Re-injection complete. Injected into {Count} of {Total} messages",
                injectedCount.ToString(),
                messagesList.Count.ToString());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ServiceBusReceiver: Error re-injecting context into ServiceBus messages");
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
}
