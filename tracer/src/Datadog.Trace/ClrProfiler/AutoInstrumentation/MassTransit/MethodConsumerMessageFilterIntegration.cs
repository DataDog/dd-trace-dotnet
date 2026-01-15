// <copyright file="MethodConsumerMessageFilterIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit;

/// <summary>
/// MassTransit.Pipeline.Filters.MethodConsumerMessageFilter`2.Send calltarget instrumentation
/// This is the internal MassTransit class that actually invokes IConsumer.Consume()
/// </summary>
[InstrumentMethod(
    AssemblyName = MassTransitConstants.MassTransitAssembly,
    TypeName = "MassTransit.Pipeline.Filters.ConsumerMessageFilter`2",
    MethodName = "GreenPipes.IFilter<MassTransit.ConsumeContext<TMessage>>.Send",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = ["_", "_"],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = MassTransitConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MethodConsumerMessageFilterIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MethodConsumerMessageFilterIntegration));

    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target (MethodConsumerMessageFilter)</typeparam>
    /// <typeparam name="TContext">Type of the ConsumerConsumeContext</typeparam>
    /// <typeparam name="TPipe">Type of the pipe</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="context">The consumer consume context.</param>
    /// <param name="next">The next pipe in the pipeline.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TContext, TPipe>(TTarget instance, TContext context, TPipe next)
    {
        Log.Debug("MassTransit MethodConsumerMessageFilterIntegration.OnMethodBegin() - Intercepted consumer message dispatch");

        string? messageType = null;
        string? consumerType = null;

        try
        {
            // Get message type from generic argument of the TARGET type (ConsumerMessageFilter<TConsumer, TMessage>)
            // The instance is the filter which has the generic args we need
            // NOTE: We must use instance.GetType() at runtime instead of typeof(TTarget)
            // because CallTarget boxing causes TTarget to be System.Object at compile time
            var targetType = instance?.GetType();
            if (targetType != null && targetType.IsGenericType)
            {
                var genericArgs = targetType.GetGenericArguments();
                if (genericArgs.Length >= 2)
                {
                    consumerType = genericArgs[0].Name;
                    messageType = genericArgs[1].Name;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to extract message/consumer type from target");
        }

        // Extract context info using duck typing
        string? destinationAddress = null;
        string? messagingSystem = "in-memory";
        PropagationContext propagationContext = default;
        IConsumerConsumeContext? duckContext = null;

        if (context is not null)
        {
            try
            {
                // Duck-cast the context to access its properties
                if (context.TryDuckCast<IConsumerConsumeContext>(out var ducked))
                {
                    duckContext = ducked;
                    var destAddr = duckContext.DestinationAddress;
                    if (destAddr != null)
                    {
                        destinationAddress = destAddr.ToString();
                        messagingSystem = DetermineMessagingSystem(destinationAddress);
                    }

                    // Try to get headers for context propagation (use reflection-based approach)
                    var headersObj = duckContext.Headers;
                    if (headersObj != null)
                    {
                        var headersAdapter = new ContextPropagation(headersObj);
                        propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headersAdapter);
                        Log.Debug("MassTransit MethodConsumerMessageFilterIntegration - Extracted propagation context from headers");
                    }
                }
                else
                {
                    var contextType = context.GetType();
                    Log.Debug("MassTransit MethodConsumerMessageFilterIntegration - Could not duck-cast context to IConsumerConsumeContext");
                    Log.Debug("MassTransit MethodConsumerMessageFilterIntegration - Context type: {ContextType}", contextType.FullName);

                    // Include inherited properties using FlattenHierarchy
                    var props = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    var propNames = string.Join(", ", props.Select(p => $"{p.Name}:{p.PropertyType.Name}").Take(30));
                    Log.Debug("MassTransit MethodConsumerMessageFilterIntegration - Available properties (with inherited): {Properties}", propNames);

                    // Also check interfaces
                    var interfaces = contextType.GetInterfaces();
                    var interfaceNames = string.Join(", ", interfaces.Select(i => i.Name).Take(15));
                    Log.Debug("MassTransit MethodConsumerMessageFilterIntegration - Implemented interfaces: {Interfaces}", interfaceNames);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to extract context info using duck typing");
            }
        }

        // Create process span - if we extracted propagation context, use it; otherwise parent under current active span
        var scope = MassTransitIntegration.CreateConsumerScope(
            Tracer.Instance,
            MassTransitConstants.OperationProcess,
            messageType ?? "Unknown",
            context: propagationContext,
            destinationName: destinationAddress,
            messagingSystem: messagingSystem);

        if (scope != null)
        {
            Log.Debug("MassTransit MethodConsumerMessageFilterIntegration - Created consumer scope for message type: {MessageType}, consumer: {ConsumerType}", messageType, consumerType);

            if (scope.Span?.Tags is MassTransitTags tags)
            {
                // Add consumer type as a tag (MT8 OTEL style)
                if (consumerType != null)
                {
                    tags.ConsumerType = consumerType;
                }

                // Set additional tags from duck-typed context
                if (duckContext != null)
                {
                    try
                    {
                        if (duckContext.SourceAddress != null)
                        {
                            tags.SourceAddress = duckContext.SourceAddress.ToString();
                        }

                        if (duckContext.MessageId.HasValue && duckContext.MessageId.Value != Guid.Empty)
                        {
                            tags.MessageId = duckContext.MessageId.Value.ToString();
                        }

                        if (duckContext.ConversationId.HasValue && duckContext.ConversationId.Value != Guid.Empty)
                        {
                            tags.ConversationId = duckContext.ConversationId.Value.ToString();
                        }

                        if (duckContext.CorrelationId.HasValue && duckContext.CorrelationId.Value != Guid.Empty)
                        {
                            tags.CorrelationId = duckContext.CorrelationId.Value.ToString();
                        }

                        if (duckContext.InitiatorId.HasValue && duckContext.InitiatorId.Value != Guid.Empty)
                        {
                            tags.InitiatorId = duckContext.InitiatorId.Value.ToString();
                        }

                        // Set peer address to match MT8 OTEL
                        if (destinationAddress != null)
                        {
                            tags.PeerAddress = destinationAddress;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to set additional consumer tags from duck-typed context");
                    }
                }
            }
        }
        else
        {
            Log.Warning("MassTransit MethodConsumerMessageFilterIntegration - Failed to create consumer scope (integration may be disabled)");
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
        Log.Debug("MassTransit MethodConsumerMessageFilterIntegration.OnAsyncMethodEnd() - Completing consume span");

        if (exception != null)
        {
            Log.Warning(exception, "MassTransit MethodConsumerMessageFilterIntegration - Consumer failed with exception");
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
