// <copyright file="ReceivePipeDispatcherIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;

/// <summary>
/// MassTransit.Transports.ReceivePipeDispatcher.Dispatch calltarget instrumentation
/// This instruments the receive pipeline to create "receive" spans for incoming messages
/// </summary>
[InstrumentMethod(
    AssemblyName = MassTransitConstants.MassTransitAssembly,
    TypeName = "MassTransit.Transports.ReceivePipeDispatcher",
    MethodName = "Dispatch",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["MassTransit.ReceiveContext", "MassTransit.Transports.ReceiveLockContext"],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = MassTransitConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ReceivePipeDispatcherIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ReceivePipeDispatcherIntegration));

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target (ReceivePipeDispatcher)</typeparam>
    /// <typeparam name="TContext">Type of the ReceiveContext</typeparam>
    /// <typeparam name="TLock">Type of the ReceiveLockContext</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="receiveLock">The receive lock context.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TContext, TLock>(TTarget instance, TContext context, TLock receiveLock)
    {
        Log.Debug("MassTransit ReceivePipeDispatcherIntegration.OnMethodBegin() - Intercepted receive dispatch");

        // Extract trace context from headers
        var propagationContext = default(PropagationContext);
        Uri? inputAddress = null;
        IReceiveContext? receiveContext = null;
        object? transportHeadersObj = null;

        // Try to duck-type the context
        if (context is not null)
        {
            try
            {
                var contextType = context.GetType();
                Log.Debug("MassTransit ReceivePipeDispatcherIntegration - Context type: {ContextType}", contextType.FullName);

                // Log available properties to debug duck typing failures
                var props = contextType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var propNames = string.Join(", ", props.Select(p => $"{p.Name}:{p.PropertyType.Name}").Take(20));
                Log.Debug("MassTransit ReceivePipeDispatcherIntegration - Available properties: {Properties}", propNames);

                if (context.TryDuckCast<IReceiveContext>(out var duckContext))
                {
                    receiveContext = duckContext;
                    inputAddress = receiveContext.InputAddress;

                    // Get transport headers
                    transportHeadersObj = receiveContext.TransportHeaders;
                    if (transportHeadersObj != null)
                    {
                        var headersType = transportHeadersObj.GetType();
                        Log.Debug("MassTransit ReceivePipeDispatcherIntegration - TransportHeaders type: {HeadersType}", headersType.FullName);

                        // Log available methods to debug duck typing failures
                        var methods = headersType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var methodSignatures = string.Join(", ", methods.Where(m => !m.IsSpecialName).Select(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})").Distinct().Take(15));
                        Log.Debug("MassTransit ReceivePipeDispatcherIntegration - TransportHeaders methods: {Methods}", methodSignatures);

                        // Use reflection-based ContextPropagation since the Get method is generic
                        var headersAdapter = new ContextPropagation(transportHeadersObj);
                        propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headersAdapter);
                        Log.Debug("MassTransit ReceivePipeDispatcherIntegration - Extracted propagation context from transport headers");
                    }
                }
                else
                {
                    Log.Debug("MassTransit ReceivePipeDispatcherIntegration - Could not duck-cast context to IReceiveContext");
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to extract receive context info");
            }
        }

        // Determine messaging system from input address
        string messagingSystem = "in-memory";
        string? queueName = null;
        if (inputAddress != null)
        {
            var addressStr = inputAddress.ToString();
            if (addressStr.IndexOf("rabbitmq://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                messagingSystem = "rabbitmq";
            }
            else if (addressStr.IndexOf("sb://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     addressStr.IndexOf("servicebus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                messagingSystem = "azureservicebus";
            }
            else if (addressStr.IndexOf("amazonsqs://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                messagingSystem = "amazonsqs";
            }
            else if (addressStr.IndexOf("kafka://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                messagingSystem = "kafka";
            }

            // Extract queue name from URI path
            queueName = inputAddress.AbsolutePath.TrimStart('/');
        }

        // Use input address as destination name for MT8 OTEL-style resource naming
        var destinationName = inputAddress?.ToString();

        var scope = MassTransitIntegration.CreateConsumerScope(
            Tracer.Instance,
            MassTransitConstants.OperationReceive,
            queueName ?? "Unknown",
            context: propagationContext,
            destinationName: destinationName,
            messagingSystem: messagingSystem);

        if (scope != null)
        {
            Log.Debug("MassTransit ReceivePipeDispatcherIntegration - Created receive scope for queue: {QueueName}", queueName);

            if (scope.Span?.Tags is MassTransitTags tags)
            {
                // Set peer address to match MT8 OTEL
                if (destinationName != null)
                {
                    tags.PeerAddress = destinationName;
                }

                // Set input_address (MT8 OTEL tag for receive spans)
                if (inputAddress != null)
                {
                    tags.InputAddress = inputAddress.ToString();
                }

                // Try to extract additional context info using duck-typed context
                if (receiveContext != null)
                {
                    try
                    {
                        // MT8 OTEL tags from TransportHeaders - extract message context info
                        // Note: TransportHeaders are raw transport-level headers. The MassTransit envelope
                        // properties (MessageId, ConversationId, etc.) are inside the message body and
                        // become available on ConsumeContext after deserialization (in process spans).
                        // Here we extract what's available at the transport level using reflection.
                        if (transportHeadersObj != null)
                        {
                            // Create a helper to read headers using reflection
                            var headerReader = new ContextPropagation(transportHeadersObj);

                            // Try various header name patterns used by different transports
                            // RabbitMQ uses different header names than in-memory transport

                            // Extract message_id - try common patterns
                            var messageId = TryExtractHeader(headerReader, "MessageId", "message_id", "message-id");
                            if (!string.IsNullOrEmpty(messageId))
                            {
                                tags.MessageId = messageId;
                            }

                            // Extract conversation_id
                            var conversationId = TryExtractHeader(headerReader, "MT-ConversationId", "ConversationId", "conversation_id");
                            if (!string.IsNullOrEmpty(conversationId))
                            {
                                tags.ConversationId = conversationId;
                            }

                            // Extract initiator_id
                            var initiatorId = TryExtractHeader(headerReader, "MT-Initiator-Id", "InitiatorId", "initiator_id");
                            if (!string.IsNullOrEmpty(initiatorId))
                            {
                                tags.InitiatorId = initiatorId;
                            }

                            // Extract request_id
                            var requestId = TryExtractHeader(headerReader, "MT-Request-Id", "RequestId", "request_id");
                            if (!string.IsNullOrEmpty(requestId))
                            {
                                tags.RequestId = requestId;
                            }

                            // Extract source_address
                            var sourceAddress = TryExtractHeader(headerReader, "MT-Source-Address", "SourceAddress", "source_address");
                            if (!string.IsNullOrEmpty(sourceAddress))
                            {
                                tags.SourceAddress = sourceAddress;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to set additional receive context tags");
                    }
                }
            }
        }
        else
        {
            Log.Warning("MassTransit ReceivePipeDispatcherIntegration - Failed to create receive scope (integration may be disabled)");
        }

        return new CallTargetState(scope);
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value</returns>
    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        Log.Debug("MassTransit ReceivePipeDispatcherIntegration.OnAsyncMethodEnd() - Completing receive span");

        if (exception != null)
        {
            Log.Warning(exception, "MassTransit ReceivePipeDispatcherIntegration - Receive dispatch failed with exception");
        }

        state.Scope.DisposeWithException(exception);
        return returnValue;
    }

    /// <summary>
    /// Tries to extract a header value from multiple possible header names using the reflection-based header reader
    /// </summary>
    private static string? TryExtractHeader(ContextPropagation headerReader, params string[] headerNames)
    {
        foreach (var headerName in headerNames)
        {
            foreach (var value in headerReader.GetValues(headerName))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
