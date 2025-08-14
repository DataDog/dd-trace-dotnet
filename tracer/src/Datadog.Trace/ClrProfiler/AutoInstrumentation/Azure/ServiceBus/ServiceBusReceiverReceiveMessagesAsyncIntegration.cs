// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

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
        var tracer = Tracer.Instance;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
        {
            return CallTargetState.GetDefault();
        }

        // Create span with correct start time, we'll set parent context later in OnAsyncMethodEnd
        var scope = tracer.StartActiveInternal(OperationName);
        var span = scope.Span;

        span.Type = SpanTypes.Queue;
        span.SetTag(Tags.SpanKind, SpanKinds.Consumer);

        var entityPath = instance.EntityPath ?? "unknown";
        span.ResourceName = entityPath;

        span.SetTag(Tags.MessagingDestinationName, entityPath);
        span.SetTag(Tags.MessagingOperation, "receive");
        span.SetTag(Tags.MessagingSystem, "servicebus");

        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        var scope = state.Scope;

        if (scope is null)
        {
            return returnValue;
        }

        try
        {
            // Log the values before checking the condition
            var hasException = exception != null;
            var returnValueType = returnValue?.GetType().FullName ?? "null";
            var isCorrectType = returnValue is IReadOnlyList<IServiceBusReceivedMessage>;
            var messageCount = returnValue is IReadOnlyList<IServiceBusReceivedMessage> msgs ? msgs.Count : -1;

            Log.Information(
                "OnAsyncMethodEnd - Exception present: {HasException}, ReturnValue type: {ReturnValueType}, Is correct type: {IsCorrectType}, Message count: {MessageCount}",
                hasException,
                returnValueType,
                isCorrectType,
                messageCount);

            // Try to extract parent context from received messages and update span if needed
            if (exception == null && returnValue is IReadOnlyList<IServiceBusReceivedMessage> messages && messages.Count > 0)
            {
                Log.Information("ServiceBus ReceiveMessagesAsync completed successfully. Received {MessageCount} messages, attempting context extraction from first message", (object)messages.Count);

                try
                {
                    var firstMessage = messages[0];
                    var messageId = firstMessage.Instance?.GetType().GetProperty("MessageId")?.GetValue(firstMessage.Instance);
                    var hasApplicationProperties = firstMessage.ApplicationProperties != null;

                    Log.Information(
                        "First message details - MessageId: {MessageId}, ApplicationProperties null: {ApplicationPropertiesNull}",
                        messageId,
                        !hasApplicationProperties);

                    if (firstMessage.ApplicationProperties != null)
                    {
                        var propertyKeys = string.Join(", ", firstMessage.ApplicationProperties.Keys);
                        Log.Information(
                            "ApplicationProperties found with {PropertyCount} properties: {PropertyKeys}",
                            (object)firstMessage.ApplicationProperties.Count,
                            propertyKeys);

                        var headerAdapter = new ServiceBusHeadersCollectionAdapter(firstMessage.ApplicationProperties);
                        var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headerAdapter);

                        // If we found a parent context, update the span's parent
                        if (extractedContext.SpanContext != null)
                        {
                            Log.Information(
                                "Successfully extracted parent context - TraceId: {TraceId}, SpanId: {SpanId}",
                                extractedContext.SpanContext.TraceId128,
                                extractedContext.SpanContext.SpanId);

                            // Note: We can't change the parent after the span is created, but we can add trace links
                            // This is a limitation of how spans work - the parent is set at creation time
                            // For now, we'll just extract the context for demonstration purposes
                        }
                        else
                        {
                            Log.Information("No parent context found in message ApplicationProperties");
                        }
                    }
                    else
                    {
                        Log.Information("ApplicationProperties is null, cannot extract context");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting context from ServiceBus message");
                }
            }
            else if (exception == null && returnValue is IReadOnlyList<IServiceBusReceivedMessage> emptyMessages && emptyMessages.Count == 0)
            {
                Log.Information("ServiceBus ReceiveMessagesAsync completed with no messages received");
            }
            else if (exception == null)
            {
                var unexpectedReturnValueType = returnValue?.GetType().FullName ?? "null";
                Log.Warning(
                    "ServiceBus ReceiveMessagesAsync completed but return value is not the expected IReadOnlyList<IServiceBusReceivedMessage> type: {ReturnValueType}",
                    unexpectedReturnValueType);
            }

            if (exception != null)
            {
                scope.Span.SetException(exception);
            }
        }
        finally
        {
            scope.Dispose();
        }

        return returnValue;
    }
}
