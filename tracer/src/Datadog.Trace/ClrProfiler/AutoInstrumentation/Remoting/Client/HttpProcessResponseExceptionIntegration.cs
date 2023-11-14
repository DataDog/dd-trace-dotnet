// <copyright file="HttpProcessResponseExceptionIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System.ComponentModel;
using System.Net;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Client
{
    /// <summary>
    /// System.Runtime.Remoting.Channels.Http.HttpClientTransportSink.ProcessResponseException calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.Http.HttpClientTransportSink",
        MethodName = "ProcessResponseException",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Net.WebException", "System.Net.HttpWebResponse&", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class HttpProcessResponseExceptionIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="webException">The web exception instance</param>
        /// <param name="response">The returned web response instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, WebException webException, ref HttpWebResponse response)
        {
            if (Tracer.Instance.InternalActiveScope is var scope)
            {
                scope.Span?.SetException(webException);
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
