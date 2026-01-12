// <copyright file="MethodConsumerMessageFilterIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
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

        // Extract trace context from headers
        var propagationContext = default(PropagationContext);
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

        // Try to extract headers for context propagation using duck typing
        if (context is not null)
        {
            try
            {
                var duckedContext = context.DuckCast<IConsumerConsumeContext>();
                if (duckedContext.Instance is not null)
                {
                    var headers = duckedContext.Headers;
                    if (headers != null)
                    {
                        var headersAdapter = new ContextPropagation(headers);
                        propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headersAdapter);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to extract propagation context from headers");
            }
        }

        var scope = MassTransitIntegration.CreateConsumerScope(
            Tracer.Instance,
            MassTransitConstants.OperationProcess,
            messageType ?? "Unknown",
            context: propagationContext);

        if (scope != null)
        {
            Log.Debug("MassTransit MethodConsumerMessageFilterIntegration - Created consumer scope for message type: {MessageType}, consumer: {ConsumerType}", messageType, consumerType);

            if (scope.Span?.Tags is MassTransitTags tags)
            {
                // Add consumer type as a tag
                if (consumerType != null)
                {
                    tags.SetTag("messaging.masstransit.consumer_type", consumerType);
                }

                // Try to extract additional context info using duck typing
                if (context is not null)
                {
                    try
                    {
                        var duckedContext = context.DuckCast<IConsumerConsumeContext>();
                        if (duckedContext.Instance is not null)
                        {
                            if (duckedContext.SourceAddress != null)
                            {
                                tags.SetTag("messaging.source.name", duckedContext.SourceAddress.ToString());
                            }

                            if (duckedContext.DestinationAddress != null)
                            {
                                tags.DestinationName = duckedContext.DestinationAddress.ToString();
                            }

                            if (duckedContext.MessageId != null)
                            {
                                tags.SetTag("messaging.message_id", duckedContext.MessageId.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to set additional consumer tags");
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
}
