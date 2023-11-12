// <copyright file="HttpReceiveAndProcessIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.Remoting.Channels;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Client
{
    /// <summary>
    /// System.Runtime.Remoting.Channels.Http.HttpClientTransportSink.ReceiveAndProcess calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.Http.HttpClientTransportSink",
        MethodName = "ReceiveAndProcess",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Net.HttpWebResponse", "System.Runtime.Remoting.Channels.ITransportHeaders&", "System.IO.Stream&", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class HttpReceiveAndProcessIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">The returned web response instance</param>
        /// <param name="returnHeaders">The returned transport headers instance</param>
        /// <param name="returnStream">The returned stream instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, HttpWebResponse response, ref ITransportHeaders returnHeaders, ref Stream returnStream)
        {
            if (Tracer.Instance.InternalActiveScope is var scope)
            {
                return new CallTargetState(scope, state: response);
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
            if (state.Scope is not null && state.State is HttpWebResponse response)
            {
                state.Scope.Span.SetHttpStatusCode((int)response.StatusCode, false, Tracer.Instance.Settings);
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
#endif
