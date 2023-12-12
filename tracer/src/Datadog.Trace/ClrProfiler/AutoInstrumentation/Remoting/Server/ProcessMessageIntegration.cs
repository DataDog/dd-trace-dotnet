// <copyright file="ProcessMessageIntegration.cs" company="Datadog">
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
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Server
{
    /// <summary>
    /// System.Runtime.Remoting.Channels.IServerChannelSink.ProcessMessage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.BinaryServerFormatterSink",
        MethodName = "ProcessMessage",
        ReturnTypeName = "System.Runtime.Remoting.Channels.ServerProcessing",
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Channels.IServerChannelSinkStack", "System.Runtime.Remoting.Messaging.IMessage", "System.Runtime.Remoting.Channels.ITransportHeaders", "System.IO.Stream", "System.Runtime.Remoting.Messaging.IMessage&", "System.Runtime.Remoting.Channels.ITransportHeaders&", "System.IO.Stream&", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.SoapServerFormatterSink",
        MethodName = "ProcessMessage",
        ReturnTypeName = "System.Runtime.Remoting.Channels.ServerProcessing",
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Channels.IServerChannelSinkStack", "System.Runtime.Remoting.Messaging.IMessage", "System.Runtime.Remoting.Channels.ITransportHeaders", "System.IO.Stream", "System.Runtime.Remoting.Messaging.IMessage&", "System.Runtime.Remoting.Channels.ITransportHeaders&", "System.IO.Stream&", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class ProcessMessageIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessMessageIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <typeparam name="TServerSinkStack">Type of the server sink stack</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="sinkStack">Server sink stack instance</param>
        /// <param name="requestMsg">The incoming request message instance</param>
        /// <param name="requestHeaders">The headers for the incoming request message</param>
        /// <param name="requestStream">The stream for the incoming request message</param>
        /// <param name="responseMsg">The outgoing response message instance</param>
        /// <param name="responseHeaders">The headers for the outgoing response message</param>
        /// <param name="responseStream">The stream for the outgoing response message</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TServerSinkStack>(TTarget instance, TServerSinkStack sinkStack, IMessage requestMsg, ITransportHeaders requestHeaders, Stream requestStream, ref IMessage responseMsg, ref ITransportHeaders responseHeaders, ref Stream responseStream)
        {
            if (requestMsg is null)
            {
                return CallTargetState.GetDefault();
            }

            // Extract span context
            SpanContext? propagatedContext = null;
            try
            {
                propagatedContext = SpanContextPropagator.Instance.Extract(requestHeaders, (headers, key) =>
                {
                    var value = headers[key];
                    return value is null ?
                        Array.Empty<string>() :
                        new string[] { value.ToString() };
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated headers.");
            }

            var scope = RemotingIntegration.CreateServerScope(requestMsg, propagatedContext);
            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            // Do not close the span here
            // The span will be closed when the message response is written
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
