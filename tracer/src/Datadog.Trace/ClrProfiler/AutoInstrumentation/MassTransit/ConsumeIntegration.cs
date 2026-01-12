// <copyright file="ConsumeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// MassTransit IConsumer.Consume calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = MassTransitConstants.MassTransitAssembly,
        TypeName = MassTransitConstants.IConsumerTypeName,
        MethodName = "Consume",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { MassTransitConstants.IConsumeContextTypeName },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = MassTransitConstants.IntegrationName,
        CallTargetIntegrationKind = CallTargetKind.Interface)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ConsumeIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ConsumeIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">The consume context.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
            where TContext : IConsumeContext, IDuckType
        {
            Log.Debug("MassTransit ConsumeIntegration.OnMethodBegin() - Intercepted IConsumer.Consume()");

            // Extract trace context from headers
            var propagationContext = default(PropagationContext);
            if (context.Instance != null && context.Headers != null)
            {
                try
                {
                    var headersAdapter = new ContextPropagation(context.Headers);
                    propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(headersAdapter);
                }
                catch
                {
                    // If extraction fails, continue without parent context
                }
            }

            // Get message type from the consumer's generic argument
            string? messageType = null;
            try
            {
                var consumerType = instance?.GetType();
                if (consumerType != null)
                {
                    var interfaces = consumerType.GetInterfaces();
                    foreach (var iface in interfaces)
                    {
                        if (iface.IsGenericType && iface.GetGenericTypeDefinition().Name.Contains("IConsumer"))
                        {
                            var genericArgs = iface.GetGenericArguments();
                            if (genericArgs.Length > 0)
                            {
                                messageType = genericArgs[0].Name;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // If we can't determine message type, use default
                messageType = "Unknown";
            }

            var scope = MassTransitIntegration.CreateConsumerScope(
                Tracer.Instance,
                MassTransitConstants.OperationProcess,
                messageType,
                context: propagationContext);

            if (scope != null)
            {
                Log.Debug("MassTransit ConsumeIntegration - Created consumer scope for message type: {MessageType}", messageType);

                if (scope.Span?.Tags is MassTransitTags tags && context.Instance != null)
                {
                    MassTransitIntegration.SetConsumeContextTags(tags, context);
                }
            }
            else
            {
                Log.Warning("MassTransit ConsumeIntegration - Failed to create consumer scope (integration may be disabled)");
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
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Log.Debug("MassTransit ConsumeIntegration.OnAsyncMethodEnd() - Completing consume span");

            if (exception != null)
            {
                Log.Warning(exception, "MassTransit ConsumeIntegration - Consume failed with exception");
            }

            state.Scope.DisposeWithException(exception);
            return returnValue;
        }
    }
}
