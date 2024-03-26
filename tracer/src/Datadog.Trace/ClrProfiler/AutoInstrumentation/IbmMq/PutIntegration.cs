// <copyright file="PutIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq
{
    /// <summary>
    /// IBM MQ Put instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = IbmMqConstants.IbmMqAssemblyName,
        TypeName = IbmMqConstants.MqDestinationTypeName,
        MethodName = "Put",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = [IbmMqConstants.MqMessageTypeName, IbmMqConstants.MqMessagePutOptionsTypeName],
        MinimumVersion = "9.0.0",
        MaximumVersion = "9.*.*",
        IntegrationName = IbmMqConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class PutIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TMessage, TOptions>(TTarget instance, TMessage msg, TOptions options)
        where TMessage : IMqMessage, IDuckType
        where TTarget : IMqQueue, IDuckType
        {
            if (instance.Instance == null || msg.Instance == null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = IbmMqHelper.CreateProducerScope(Tracer.Instance, instance, msg);
            if (scope is not null)
            {
                var dataStreams = Tracer.Instance.TracerManager.DataStreamsManager;
                if (dataStreams.IsEnabled && (instance).Instance != null && (msg).Instance != null)
                {
                    var edgeTags = new[] { "direction:out", $"topic:{instance.Name}", $"type:{IbmMqConstants.QueueType}" };
                    scope.Span.SetDataStreamsCheckpoint(dataStreams, CheckpointKind.Produce, edgeTags, msg.MessageLength, 0);
                    dataStreams.InjectPathwayContextAsBase64String(scope.Span.Context.PathwayContext, IbmMqHelper.GetHeadersAdapter(msg));
                }

                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
