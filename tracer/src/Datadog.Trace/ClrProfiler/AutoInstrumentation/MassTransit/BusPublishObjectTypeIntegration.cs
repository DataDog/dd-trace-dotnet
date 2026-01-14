// <copyright file="BusPublishObjectTypeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// MassTransit MassTransitBus.Publish(object, Type) calltarget instrumentation
    /// NOTE: This instrumentation is DISABLED to match MT8 OTEL behavior.
    /// MT8 OTEL does not create a separate "publish" span - only the "send" span is created.
    /// The SendEndpointPipeSendIntegration captures all send operations including publishes.
    /// </summary>
    // [InstrumentMethod(
    //     AssemblyName = MassTransitConstants.MassTransitAssembly,
    //     TypeName = MassTransitConstants.IPublishEndpointTypeName,
    //     MethodName = "Publish",
    //     ReturnTypeName = ClrNames.Task,
    //     ParameterTypeNames = new[] { ClrNames.Object, ClrNames.Type, ClrNames.CancellationToken },
    //     MinimumVersion = "7.0.0",
    //     MaximumVersion = "7.*.*",
    //     IntegrationName = MassTransitConstants.IntegrationName,
    //     CallTargetIntegrationKind = CallTargetKind.Interface)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class BusPublishObjectTypeIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BusPublishObjectTypeIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="message">The message being published (as object).</param>
        /// <param name="messageType">The message type</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object message, Type messageType, CancellationToken cancellationToken)
        {
            Log.Debug("MassTransit BusPublishObjectTypeIntegration.OnMethodBegin() - Intercepted Publish(object, Type)");

            var messageTypeName = messageType?.Name ?? message?.GetType().Name ?? "Unknown";
            var messageTypeFullName = messageType?.FullName ?? message?.GetType().FullName ?? "Unknown";

            var scope = MassTransitIntegration.CreateProducerScope(
                Tracer.Instance,
                MassTransitConstants.OperationPublish,
                messageTypeName,
                destinationName: $"urn:message:{messageTypeFullName}");

            if (scope != null)
            {
                Log.Debug("MassTransit BusPublishObjectTypeIntegration - Created producer scope for message type: {MessageType}", messageTypeName);
            }
            else
            {
                Log.Warning("MassTransit BusPublishObjectTypeIntegration - Failed to create producer scope (integration may be disabled)");
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
            Log.Debug("MassTransit BusPublishObjectTypeIntegration.OnAsyncMethodEnd() - Completing publish span");

            if (exception != null)
            {
                Log.Warning(exception, "MassTransit BusPublishObjectTypeIntegration - Publish failed with exception");
            }

            state.Scope.DisposeWithException(exception);
            return returnValue;
        }
    }
}
