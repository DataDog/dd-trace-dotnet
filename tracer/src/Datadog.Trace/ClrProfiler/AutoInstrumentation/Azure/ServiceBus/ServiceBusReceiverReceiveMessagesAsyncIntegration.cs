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

        // Store start time and instance for use in OnAsyncMethodEnd where we'll create the span with proper parent context
        var startTime = DateTimeOffset.UtcNow;
        return new CallTargetState(null, new ReceiveMessagesState(instance, startTime));
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Log.Information("ServiceBusReceiverReceiveMessagesAsyncIntegration.OnAsyncMethodEnd called");
        var tracer = Tracer.Instance;
        if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus) || state.State == null)
        {
            return returnValue;
        }

        // Extract the stored data from OnMethodBegin
        if (!(state.State is ReceiveMessagesState stateData))
        {
            return returnValue;
        }

        var receiverInstance = stateData.Instance;
        var startTime = stateData.StartTime;

        // Extract parent context first if messages are available
        SpanContext? parentContext = null;

        if (exception == null && returnValue is System.Collections.IList messageList && messageList.Count > 0)
        {
            try
            {
                // Duck cast the first message to extract context
                var firstMessageObj = messageList[0];
                if (firstMessageObj?.TryDuckCast<IServiceBusReceivedMessage>(out var firstMessage) == true)
                {
                    if (firstMessage.ApplicationProperties != null)
                    {
                        var headerAdapter = new ServiceBusHeadersCollectionAdapter(firstMessage.ApplicationProperties);
                        var extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headerAdapter);
                        parentContext = extractedContext.SpanContext;

                        if (parentContext != null)
                        {
                            Log.Information(
                                "Successfully extracted parent context - TraceId: {TraceId}, SpanId: {SpanId}",
                                parentContext.TraceId128,
                                parentContext.SpanId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting parent context from ServiceBus message");
            }
        }

        // Log the values for debugging
        var hasException = exception != null;
        var returnValueType = returnValue?.GetType().FullName ?? "null";
        var isCorrectType = returnValue is IReadOnlyList<IServiceBusReceivedMessage>;
        var isListType = returnValue is System.Collections.IList;
        var messageCount = returnValue is System.Collections.ICollection collection ? collection.Count : -1;

        Log.Information("OnAsyncMethodEnd - Exception present: {HasException}", hasException);
        Log.Information("OnAsyncMethodEnd - ReturnValue type: {ReturnValueType}", returnValueType);
        Log.Information("OnAsyncMethodEnd - Is duck type: {IsCorrectType}", isCorrectType);
        Log.Information("OnAsyncMethodEnd - Is IList: {IsListType}", isListType);
        Log.Information("OnAsyncMethodEnd - Message count: {MessageCount}", (object)messageCount);
        Log.Information("OnAsyncMethodEnd - Parent context extracted: {HasParent}", parentContext != null);

        // Only create span if we have messages or an exception worth tracking
        var shouldCreateSpan = (exception == null && returnValue is System.Collections.IList msgList && msgList.Count > 0) ||
                               (exception != null);

        if (shouldCreateSpan)
        {
            // Create span with the extracted parent context
            var scope = tracer.StartActiveInternal(OperationName, parent: parentContext);
            var span = scope.Span;

            try
            {
                // Set span properties
                span.Type = SpanTypes.Queue;
                span.SetTag(Tags.SpanKind, SpanKinds.Consumer);

                var entityPath = receiverInstance.EntityPath ?? "unknown";
                span.ResourceName = entityPath;

                span.SetTag(Tags.MessagingDestinationName, entityPath);
                span.SetTag(Tags.MessagingOperation, "receive");
                span.SetTag(Tags.MessagingSystem, "servicebus");

                // Additional detailed logging for the messages (already extracted context above)
                if (exception == null && returnValue is System.Collections.IList successMsgList && successMsgList.Count > 0)
                {
                    Log.Information("ServiceBus ReceiveMessagesAsync completed successfully. Received {MessageCount} messages with parent context: {HasParent}", (object)successMsgList.Count, parentContext != null);
                }

                if (exception != null)
                {
                    span.SetException(exception);
                    Log.Information("ServiceBus ReceiveMessagesAsync failed with exception, span created to track the error");
                }
            }
            finally
            {
                scope.Dispose();
            }
        }
        else
        {
            // No span created for successful operations with no messages
            if (exception == null && returnValue is System.Collections.IList emptyMessages && emptyMessages.Count == 0)
            {
                Log.Information("ServiceBus ReceiveMessagesAsync completed with no messages received - no span created");
            }
            else if (exception == null)
            {
                var unexpectedReturnValueType = returnValue?.GetType().FullName ?? "null";
                Log.Warning(
                    "ServiceBus ReceiveMessagesAsync completed but return value is not the expected IList type: {ReturnValueType} - no span created",
                    unexpectedReturnValueType);
            }
        }

        return returnValue;
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
