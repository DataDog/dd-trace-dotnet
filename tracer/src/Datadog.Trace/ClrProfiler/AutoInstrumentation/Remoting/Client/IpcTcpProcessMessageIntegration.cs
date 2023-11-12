// <copyright file="IpcTcpProcessMessageIntegration.cs" company="Datadog">
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
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Client
{
    /// <summary>
    /// System.Runtime.Remoting.Channels.IClientChannelSink.ProcessMessage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.Ipc.IpcClientTransportSink",
        MethodName = "ProcessMessage",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Messaging.IMessage", "System.Runtime.Remoting.Channels.ITransportHeaders", "System.IO.Stream", "System.Runtime.Remoting.Channels.ITransportHeaders&", "System.IO.Stream&", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.Tcp.TcpClientTransportSink",
        MethodName = "ProcessMessage",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Messaging.IMessage", "System.Runtime.Remoting.Channels.ITransportHeaders", "System.IO.Stream", "System.Runtime.Remoting.Channels.ITransportHeaders&", "System.IO.Stream&", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class IpcTcpProcessMessageIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="msg">The incoming request message instance</param>
        /// <param name="requestHeaders">The headers for the incoming request message</param>
        /// <param name="requestStream">The stream for the incoming request message</param>
        /// <param name="responseHeaders">The headers for the outgoing response message</param>
        /// <param name="responseStream">The stream for the outgoing response message</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IMessage msg, ITransportHeaders requestHeaders, Stream requestStream, ref ITransportHeaders responseHeaders, ref Stream responseStream)
        {
            var tracer = Tracer.Instance;
            if (tracer.Settings.IsIntegrationEnabled(RemotingIntegration.IntegrationId) && tracer.InternalActiveScope is var scope)
            {
                SpanContextPropagator.Instance.Inject(scope.Span.Context, requestHeaders, (headers, key, value) => headers[key] = value);
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
