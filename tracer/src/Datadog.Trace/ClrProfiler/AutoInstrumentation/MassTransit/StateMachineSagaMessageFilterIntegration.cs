// <copyright file="StateMachineSagaMessageFilterIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;

/// <summary>
/// Automatonymous.Pipeline.StateMachineSagaMessageFilter`2.Send calltarget instrumentation
/// This instruments saga state machine message processing in MassTransit 7
/// </summary>
[InstrumentMethod(
    AssemblyName = MassTransitConstants.MassTransitAssembly,
    TypeName = "Automatonymous.Pipeline.StateMachineSagaMessageFilter`2",
    MethodName = "Send",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["_", "_"],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = MassTransitConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class StateMachineSagaMessageFilterIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StateMachineSagaMessageFilterIntegration));

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TContext">Type of the SagaConsumeContext</typeparam>
    /// <typeparam name="TPipe">Type of the pipe</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="context">The saga consume context.</param>
    /// <param name="next">The next pipe in the pipeline.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TContext, TPipe>(TTarget instance, TContext context, TPipe next)
    {
        Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration.OnMethodBegin() - Intercepted saga state machine");

        string? messageType = null;
        string? sagaType = null;
        Guid? correlationId = null;
        string? destinationAddress = null;
        string? messagingSystem = "in-memory";
        string? beginState = null;
        object? sagaInstance = null;

        try
        {
            // Get saga and message types from generic arguments of the TARGET type
            var targetType = instance?.GetType();
            if (targetType != null && targetType.IsGenericType)
            {
                var genericArgs = targetType.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    sagaType = genericArgs[0].Name;
                    messageType = genericArgs[1].Name;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to extract message/saga type from target");
        }

        // Try to extract context info using reflection
        if (context is not null)
        {
            try
            {
                var contextType = context.GetType();

                // Try to get CorrelationId from the saga context
                var correlationIdProp = contextType.GetProperty("CorrelationId");
                if (correlationIdProp?.GetValue(context) is Guid id)
                {
                    correlationId = id;
                }

                // Try to get the saga instance to extract current state
                var sagaProp = contextType.GetProperty("Saga");
                if (sagaProp != null)
                {
                    sagaInstance = sagaProp.GetValue(context);
                    if (sagaInstance != null)
                    {
                        // Try to get CurrentState property from the saga
                        var currentStateProp = sagaInstance.GetType().GetProperty("CurrentState");
                        if (currentStateProp != null)
                        {
                            var currentState = currentStateProp.GetValue(sagaInstance);
                            beginState = currentState?.ToString();
                            Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration - Begin state: {BeginState}", beginState);
                        }
                    }
                }

                // Get destination address for messaging system detection via MessageContext interface
                var messageContextInterface = contextType.GetInterface("MassTransit.MessageContext");
                if (messageContextInterface != null)
                {
                    var destAddressProp = messageContextInterface.GetProperty("DestinationAddress");
                    if (destAddressProp?.GetValue(context) is Uri destAddress)
                    {
                        destinationAddress = destAddress.ToString();
                        messagingSystem = DetermineMessagingSystem(destinationAddress);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to extract saga info");
            }
        }

        // NOTE: We do NOT extract trace context from headers here.
        // The "process" span should be a child of the "receive" span which is already active.
        // The "receive" span (created by ReceivePipeDispatcherIntegration) already extracted
        // the trace context from headers, so we just parent under the current active span.
        var scope = MassTransitIntegration.CreateConsumerScope(
            Tracer.Instance,
            MassTransitConstants.OperationProcess,
            messageType ?? "Unknown",
            context: default, // Parent under current active span (the receive span)
            destinationName: destinationAddress,
            messagingSystem: messagingSystem);

        if (scope != null)
        {
            Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration - Created saga scope for message type: {MessageType}, saga: {SagaType}", messageType, sagaType);

            if (scope.Span?.Tags is MassTransitTags tags)
            {
                // Add saga-specific tags (MT8 OTEL style)
                if (sagaType != null)
                {
                    tags.SetTag("messaging.masstransit.saga_type", sagaType);
                    // MT8 OTEL uses consumer_type for the state machine name
                    tags.ConsumerType = sagaType + "StateMachine";
                }

                if (correlationId.HasValue)
                {
                    tags.SagaId = correlationId.Value.ToString();
                    tags.CorrelationId = correlationId.Value.ToString();
                }

                // Set begin state (MT8 OTEL tag)
                if (beginState != null)
                {
                    tags.BeginState = beginState;
                }

                // Set peer address to match MT8 OTEL
                if (destinationAddress != null)
                {
                    tags.PeerAddress = destinationAddress;
                }

                // Try to extract additional context info
                if (context is not null)
                {
                    try
                    {
                        var contextType = context.GetType();
                        var messageContextInterface = contextType.GetInterface("MassTransit.MessageContext");
                        if (messageContextInterface != null)
                        {
                            var messageIdProp = messageContextInterface.GetProperty("MessageId");
                            var conversationIdProp = messageContextInterface.GetProperty("ConversationId");
                            var sourceAddressProp = messageContextInterface.GetProperty("SourceAddress");

                            if (messageIdProp?.GetValue(context) is Guid messageId)
                            {
                                tags.MessageId = messageId.ToString();
                            }

                            if (conversationIdProp?.GetValue(context) is Guid conversationId)
                            {
                                tags.ConversationId = conversationId.ToString();
                            }

                            // MT8 OTEL tag: source_address
                            if (sourceAddressProp?.GetValue(context) is Uri sourceAddress)
                            {
                                tags.SourceAddress = sourceAddress.ToString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to set additional saga context tags");
                    }
                }
            }

            // Store saga instance in state to capture end state later
            return new CallTargetState(scope, sagaInstance);
        }
        else
        {
            Log.Warning("MassTransit StateMachineSagaMessageFilterIntegration - Failed to create saga scope (integration may be disabled)");
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
        Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration.OnAsyncMethodEnd() - Completing saga span");

        if (exception != null)
        {
            Log.Warning(exception, "MassTransit StateMachineSagaMessageFilterIntegration - Saga state machine processing failed with exception");
        }

        // Try to capture end state (MT8 OTEL tag)
        if (state.Scope?.Span?.Tags is MassTransitTags tags && state.State != null)
        {
            try
            {
                var sagaInstance = state.State;
                var currentStateProp = sagaInstance.GetType().GetProperty("CurrentState");
                if (currentStateProp != null)
                {
                    var endState = currentStateProp.GetValue(sagaInstance)?.ToString();
                    if (endState != null)
                    {
                        tags.EndState = endState;
                        Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration - End state: {EndState}", endState);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to capture saga end state");
            }
        }

        state.Scope.DisposeWithException(exception);
        return returnValue;
    }

    private static string DetermineMessagingSystem(string? destination)
    {
        if (string.IsNullOrEmpty(destination))
        {
            return "in-memory";
        }

        if (destination!.IndexOf("rabbitmq://", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "rabbitmq";
        }

        if (destination.IndexOf("sb://", StringComparison.OrdinalIgnoreCase) >= 0 ||
            destination.IndexOf("servicebus", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "azureservicebus";
        }

        if (destination.IndexOf("amazonsqs://", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "amazonsqs";
        }

        if (destination.IndexOf("kafka://", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "kafka";
        }

        if (destination.IndexOf("loopback://", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "in-memory";
        }

        return "in-memory";
    }
}
