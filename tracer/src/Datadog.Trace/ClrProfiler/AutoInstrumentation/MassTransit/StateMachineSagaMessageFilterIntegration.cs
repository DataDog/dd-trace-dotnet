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
///
/// This integration adds saga-specific tags to the existing process span (created by DatadogConsumeFilter)
/// rather than creating a new nested span. This matches MT8 OTEL behavior which has a single process span
/// with saga tags (saga_type, saga_id, begin_state, end_state, consumer_type, correlation_id).
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

        // Get the active span (the process span from DatadogConsumeFilter)
        // We don't create a new span - just add saga tags to the existing one to match MT8 OTEL behavior
        var activeScope = Tracer.Instance.InternalActiveScope;
        if (activeScope?.Span?.Tags is not MassTransitTags tags)
        {
            Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration - No active MassTransit span found, skipping saga tag addition");
            return default;
        }

        string? sagaType = null;
        object? sagaInstance = null;

        try
        {
            // Get saga type from generic arguments of the TARGET type (StateMachineSagaMessageFilter<TSaga, TMessage>)
            var targetType = instance?.GetType();
            if (targetType != null && targetType.IsGenericType)
            {
                var genericArgs = targetType.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    sagaType = genericArgs[0].Name;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to extract saga type from target");
        }

        // Try to extract saga info from context using reflection
        if (context is not null)
        {
            try
            {
                var contextType = context.GetType();

                // Try to get CorrelationId from the saga context
                var correlationIdProp = contextType.GetProperty("CorrelationId");
                if (correlationIdProp?.GetValue(context) is Guid correlationId)
                {
                    tags.SagaId = correlationId.ToString();
                    tags.CorrelationId = correlationId.ToString();
                }

                // Try to get the saga instance to extract current state
                var sagaProp = contextType.GetProperty("Saga");
                if (sagaProp != null)
                {
                    sagaInstance = sagaProp.GetValue(context);
                    if (sagaInstance != null)
                    {
                        // Use saga instance type if we couldn't get it from target
                        if (sagaType == null)
                        {
                            sagaType = sagaInstance.GetType().Name;
                        }

                        // Try to get CurrentState property from the saga for begin_state
                        var currentStateProp = sagaInstance.GetType().GetProperty("CurrentState");
                        if (currentStateProp != null)
                        {
                            var currentState = currentStateProp.GetValue(sagaInstance);
                            if (currentState != null)
                            {
                                tags.BeginState = currentState.ToString();
                                Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration - Begin state: {BeginState}", tags.BeginState);
                            }
                        }
                    }
                }

                // Get destination address for peer.address tag (MT8 OTEL style)
                var messageContextInterface = contextType.GetInterface("MassTransit.MessageContext");
                if (messageContextInterface != null)
                {
                    var destAddressProp = messageContextInterface.GetProperty("DestinationAddress");
                    if (destAddressProp?.GetValue(context) is Uri destAddress)
                    {
                        tags.PeerAddress = destAddress.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to extract saga info");
            }
        }

        // Set saga-specific tags (MT8 OTEL style)
        if (sagaType != null)
        {
            tags.SagaType = sagaType;
            // MT8 OTEL uses consumer_type for the state machine name
            tags.ConsumerType = sagaType + "StateMachine";
            Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration - Added saga tags to existing span: saga_type={SagaType}", sagaType);
        }

        // Store saga instance in state to capture end state later (no scope - we're not managing it)
        return new CallTargetState(scope: null, sagaInstance);
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
        Log.Debug("MassTransit StateMachineSagaMessageFilterIntegration.OnAsyncMethodEnd() - Capturing saga end state");

        // Get the active span (the process span from DatadogConsumeFilter)
        // We capture end_state on the existing span
        var activeScope = Tracer.Instance.InternalActiveScope;

        // Try to capture end state (MT8 OTEL tag) from the saga instance stored in state
        if (state.State != null && activeScope?.Span?.Tags is MassTransitTags tags)
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

        // Note: We don't dispose any scope here - the DatadogConsumeFilter manages the process span lifecycle
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
