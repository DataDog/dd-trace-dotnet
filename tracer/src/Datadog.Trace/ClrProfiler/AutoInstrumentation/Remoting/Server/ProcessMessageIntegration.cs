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
    public sealed class ProcessMessageIntegration
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
            // Several sinks of the same chain can run for one request, but there is only one server scope:
            // the first ProcessMessage creates it, and later ones reuse it (and may now have the message).
            if (sinkStack is not null && RemotingIntegration.TryGetServerScope(sinkStack, out var existingScope) && existingScope is not null)
            {
                RemotingIntegration.SetMethodNameIfMissing(existingScope, requestMsg);
                return CallTargetState.GetDefault();
            }

            // Extract span context. Headers are available regardless of whether requestMsg is populated.
            PropagationContext extractedContext = default;

            try
            {
                extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator
                                                        .Extract(requestHeaders, (headers, key) => headers[key] is { } value ? [value.ToString()] : [])
                                                        .MergeBaggageInto(Baggage.Current);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting propagated headers.");
            }

            // requestMsg can be null here (a formatter that still has to deserialize the request). Create the
            // scope anyway, here and not in SerializeResponse: the customer's remote method runs in between,
            // and it needs this to be the ambient scope so its own spans parent correctly.
            var scope = RemotingIntegration.CreateServerScope(requestMsg, extractedContext);

            if (sinkStack is not null)
            {
                RemotingIntegration.SetServerScope(sinkStack, scope);
            }

            // Passing the sinkStack as state marks this call as the owner of the scope (see OnMethodEnd).
            return new CallTargetState(scope, sinkStack);
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
            // Only the call that created the scope (the one that carries the sinkStack as state) cleans up, and
            // only once the whole chain is done: a nested sink returns before the outer one serializes the response.
            // Normally SerializeResponse has already taken and closed the scope by now, or will do so later for an
            // Async response. If the scope is still stored, no response went through SerializeResponse (one-way
            // call, exception, or a formatter that caught a failure and serialized the error itself): close it here.
            if (state.State is { } sinkStack)
            {
                var isAsync = returnValue is ServerProcessing processing && processing == ServerProcessing.Async;

                if ((exception is not null || !isAsync)
                    && RemotingIntegration.TryGetAndRemoveServerScope(sinkStack, out var scope))
                {
                    scope.DisposeWithException(exception);
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
