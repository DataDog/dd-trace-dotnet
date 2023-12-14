// <copyright file="HttpProcessAndSendIntegration.cs" company="Datadog">
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
using System.Runtime.Remoting.Messaging;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Client
{
    /// <summary>
    /// System.Runtime.Remoting.Channels.Http.HttpClientTransportSink.ProcessAndSend calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.Http.HttpClientTransportSink",
        MethodName = "ProcessAndSend",
        ReturnTypeName = "System.Net.HttpWebRequest",
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Messaging.IMessage", "System.Runtime.Remoting.Channels.ITransportHeaders", "System.IO.Stream", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class HttpProcessAndSendIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="msg">The incoming request message instance</param>
        /// <param name="headers">The headers for the incoming request message</param>
        /// <param name="inputStream">The stream for the incoming request message</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, IMessage msg, ITransportHeaders headers, Stream inputStream)
        {
            if (Tracer.Instance.InternalActiveScope is var scope)
            {
                return new CallTargetState(scope);
            }

            return CallTargetState.GetDefault();
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
            if (state.Scope?.Span is Span span && span.Tags is HttpTags httpTags && returnValue is HttpWebRequest request)
            {
                var requestUri = request.RequestUri;
                var requestMethod = request.Method.ToUpperInvariant();

                if (requestUri != null)
                {
                    httpTags.HttpUrl = UriHelpers.CleanUri(requestUri, removeScheme: false, tryRemoveIds: false);

                    string resourceUrl = UriHelpers.CleanUri(requestUri, removeScheme: true, tryRemoveIds: true);
                    span.ResourceName = $"{requestMethod} {resourceUrl}";
                }
                else
                {
                    span.ResourceName = requestMethod;
                }

                httpTags.HttpMethod = requestMethod;
                httpTags.Host = HttpRequestUtils.GetNormalizedHost(requestUri?.Host);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
#endif
