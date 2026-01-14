// <copyright file="ReceivePipeDispatcherIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Reflection;
using Datadog.Trace.ClrProfiler.CallTarget;
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

        // Try to extract headers and context info using reflection
        if (context is not null)
        {
            try
            {
                var contextType = context.GetType();

                // Get InputAddress from ReceiveContext
                var inputAddressProp = contextType.GetProperty("InputAddress");
                if (inputAddressProp?.GetValue(context) is Uri addr)
                {
                    inputAddress = addr;
                }

                // Get TransportHeaders for trace context propagation
                var transportHeadersProp = contextType.GetProperty("TransportHeaders");
                if (transportHeadersProp != null)
                {
                    var headersObj = transportHeadersProp.GetValue(context);
                    if (headersObj != null)
                    {
                        Log.Debug("MassTransit ReceivePipeDispatcherIntegration - TransportHeaders type: {HeadersType}", headersObj.GetType().FullName);

                        var headersType = headersObj.GetType();
                        var headersInterface = headersType.GetInterface("MassTransit.Headers");
                        if (headersInterface != null)
                        {
                            var tryGetHeaderMethod = headersInterface.GetMethod("TryGetHeader");
                            if (tryGetHeaderMethod != null)
                            {
                                var headersAdapter = new ReflectionHeadersAdapter(headersObj, tryGetHeaderMethod);
                                propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headersAdapter);
                                Log.Debug("MassTransit ReceivePipeDispatcherIntegration - Extracted propagation context from transport headers");
                            }
                        }
                    }
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

                // Try to extract additional context info
                if (context is not null)
                {
                    try
                    {
                        var contextType = context.GetType();

                        // Get body length if available (MT8 OTEL tag)
                        var bodyLengthProp = contextType.GetProperty("ContentLength") ?? contextType.GetProperty("BodyLength");
                        if (bodyLengthProp != null)
                        {
                            var bodyLength = bodyLengthProp.GetValue(context);
                            if (bodyLength != null)
                            {
                                tags.MessageSize = bodyLength.ToString() ?? "0";
                            }
                        }

                        // Get redelivered status
                        var redeliveredProp = contextType.GetProperty("Redelivered");
                        if (redeliveredProp?.GetValue(context) is bool redelivered && redelivered)
                        {
                            tags.SetTag("messaging.masstransit.redelivered", "true");
                        }

                        // MT8 OTEL tags from TransportHeaders - extract message context info
                        // Note: TransportHeaders are raw transport-level headers. The MassTransit envelope
                        // properties (MessageId, ConversationId, etc.) are inside the message body and
                        // become available on ConsumeContext after deserialization (in process spans).
                        // Here we extract what's available at the transport level.
                        var transportHeadersProp = contextType.GetProperty("TransportHeaders");
                        if (transportHeadersProp != null)
                        {
                            var headersObj = transportHeadersProp.GetValue(context);
                            if (headersObj != null)
                            {
                                var headersType = headersObj.GetType();
                                var headersInterface = headersType.GetInterface("MassTransit.Headers");
                                if (headersInterface != null)
                                {
                                    var tryGetHeaderMethod = headersInterface.GetMethod("TryGetHeader");
                                    if (tryGetHeaderMethod != null)
                                    {
                                        // Try various header name patterns used by different transports
                                        // RabbitMQ uses different header names than in-memory transport

                                        // Extract message_id - try common patterns
                                        var messageIdHeaders = new[] { "MessageId", "message_id", "message-id" };
                                        foreach (var headerName in messageIdHeaders)
                                        {
                                            var args = new object?[] { headerName, null };
                                            if ((bool)(tryGetHeaderMethod.Invoke(headersObj, args) ?? false) && args[1] != null)
                                            {
                                                tags.MessageId = args[1]?.ToString();
                                                break;
                                            }
                                        }

                                        // Extract conversation_id
                                        var convIdHeaders = new[] { "MT-ConversationId", "ConversationId", "conversation_id" };
                                        foreach (var headerName in convIdHeaders)
                                        {
                                            var args = new object?[] { headerName, null };
                                            if ((bool)(tryGetHeaderMethod.Invoke(headersObj, args) ?? false) && args[1] != null)
                                            {
                                                tags.ConversationId = args[1]?.ToString();
                                                break;
                                            }
                                        }

                                        // Extract initiator_id
                                        var initiatorHeaders = new[] { "MT-Initiator-Id", "InitiatorId", "initiator_id" };
                                        foreach (var headerName in initiatorHeaders)
                                        {
                                            var args = new object?[] { headerName, null };
                                            if ((bool)(tryGetHeaderMethod.Invoke(headersObj, args) ?? false) && args[1] != null)
                                            {
                                                tags.InitiatorId = args[1]?.ToString();
                                                break;
                                            }
                                        }

                                        // Extract request_id
                                        var requestIdHeaders = new[] { "MT-Request-Id", "RequestId", "request_id" };
                                        foreach (var headerName in requestIdHeaders)
                                        {
                                            var args = new object?[] { headerName, null };
                                            if ((bool)(tryGetHeaderMethod.Invoke(headersObj, args) ?? false) && args[1] != null)
                                            {
                                                tags.RequestId = args[1]?.ToString();
                                                break;
                                            }
                                        }

                                        // Extract source_address
                                        var sourceAddrHeaders = new[] { "MT-Source-Address", "SourceAddress", "source_address" };
                                        foreach (var headerName in sourceAddrHeaders)
                                        {
                                            var args = new object?[] { headerName, null };
                                            if ((bool)(tryGetHeaderMethod.Invoke(headersObj, args) ?? false) && args[1] != null)
                                            {
                                                tags.SourceAddress = args[1]?.ToString();
                                                break;
                                            }
                                        }
                                    }
                                }
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
}
