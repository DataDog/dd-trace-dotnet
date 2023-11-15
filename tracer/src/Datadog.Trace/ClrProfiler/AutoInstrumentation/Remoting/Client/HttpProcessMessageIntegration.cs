// <copyright file="HttpProcessMessageIntegration.cs" company="Datadog">
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
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Client
{
    /// <summary>
    /// System.Runtime.Remoting.Channels.IClientChannelSink.ProcessMessage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "System.Runtime.Remoting",
        TypeName = "System.Runtime.Remoting.Channels.Http.HttpClientTransportSink",
        MethodName = "ProcessMessage",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Runtime.Remoting.Messaging.IMessage", "System.Runtime.Remoting.Channels.ITransportHeaders", "System.IO.Stream", "System.Runtime.Remoting.Channels.ITransportHeaders&", "System.IO.Stream&", },
        MinimumVersion = RemotingIntegration.Major4,
        MaximumVersion = RemotingIntegration.Major4,
        IntegrationName = RemotingIntegration.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class HttpProcessMessageIntegration
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
            // Create a new trace_id/span_id for the child WebRequest span
            var tracer = Tracer.Instance;
            if (tracer.Settings.IsIntegrationEnabled(WebRequestCommon.IntegrationId))
            {
                // If we create a scope, we can update the following attributes in other automatic instrumentation methods:
                // - Method
                // - Uri
                // - ResourceName (Recalculate with both Method and Uri)

                Scope scope = ScopeFactory.CreateOutboundHttpScope(tracer, httpMethod: null, requestUri: null, WebRequestCommon.IntegrationId, out _);
                if (scope != null)
                {
                    // Add distributed tracing headers to the HTTP request.
                    // The expected sequence of calls is GetRequestStream -> GetResponse. Headers can't be modified after calling GetRequestStream.
                    // At the same time, we don't want to set an active scope now, because it's possible that GetResponse will never be called.
                    // Instead, we generate a spancontext and inject it in the headers. GetResponse will fetch them and create an active scope with the right id.
                    SpanContextPropagator.Instance.Inject(scope.Span.Context, requestHeaders, (headers, key, value) => headers[key] = value);

                    // "Disable" tracing so that the regular WebRequest instrumentation does not fire for this request
                    requestHeaders["x-datadog-tracing-enabled"] = "false";

                    tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(WebRequestCommon.IntegrationId);
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
