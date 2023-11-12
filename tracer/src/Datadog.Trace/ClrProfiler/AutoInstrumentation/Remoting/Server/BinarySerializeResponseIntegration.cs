// <copyright file="BinarySerializeResponseIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Server
{
    /// <summary>
    /// System.Runtime.Remoting.Channels.BinaryServerFormatterSink.SerializeResponse calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.BinaryServerFormatterSink",
        MethodName = "SerializeResponse",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Channels.IServerResponseChannelSinkStack", "System.Runtime.Remoting.Messaging.IMessage", "System.Runtime.Remoting.Channels.ITransportHeaders&", "System.IO.Stream&", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class BinarySerializeResponseIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="sinkStack">The sink stack instance</param>
        /// <param name="msg">The incoming request message instance</param>
        /// <param name="headers">The headers for the outgoing response message</param>
        /// <param name="stream">The stream for the outgoing response message</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IServerResponseChannelSinkStack sinkStack, IMessage msg, ref ITransportHeaders headers, ref Stream stream)
        {
            if (msg is IMethodReturnMessage methodReturnMessage)
            {
                var scope = Tracer.Instance.InternalActiveScope;
                if (scope?.Span.Tags is RemotingTags tags)
                {
                    if (methodReturnMessage.Exception is Exception exception)
                    {
                        scope.Span.SetException(exception);
                    }

                    return new CallTargetState(scope);
                }
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
        /// <returns>A response value</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
#endif
