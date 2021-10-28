// <copyright file="WebRequestIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for WebRequest.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class WebRequestIntegration
    {
        private const string WebRequestTypeName = "System.Net.WebRequest";
        private const string Major2 = "2";
        private const string Major4 = "4";
        private const string Major5 = "5";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.WebRequest));
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WebRequestIntegration));

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetRequestStream"/>.
        /// </summary>
        /// <param name="webRequest">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System", // .NET Framework
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { "System.IO.Stream" },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssembly = "System.Net.Requests", // .NET Core
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { "System.IO.Stream" },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        public static object GetRequestStream(object webRequest, int opCode, int mdToken, long moduleVersionPtr)
        {
            const string methodName = nameof(GetRequestStream);

            Func<object, Stream> callGetRequestStream;

            try
            {
                var instrumentedType = webRequest.GetInstrumentedType(WebRequestTypeName);
                callGetRequestStream =
                    MethodBuilder<Func<object, Stream>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(instrumentedType)
                       .WithNamespaceAndNameFilters("System.IO.Stream")
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: WebRequestTypeName,
                    methodName: methodName,
                    instanceType: webRequest.GetType().AssemblyQualifiedName);
                throw;
            }

            var request = (WebRequest)webRequest;

            if (!(request is HttpWebRequest) || !IsTracingEnabled(request))
            {
                return callGetRequestStream(webRequest);
            }

            InjectHeadersForGetRequestStream(request);

            return callGetRequestStream(webRequest);
        }

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.BeginGetRequestStream"/>.
        /// </summary>
        /// <param name="webRequest">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <param name="callback">The callback parameter</param>
        /// <param name="state">The state parameter</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System", // .NET Framework
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { ClrNames.IAsyncResult, ClrNames.AsyncCallback, ClrNames.Object },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssembly = "System.Net.Requests", // .NET Core
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { ClrNames.IAsyncResult, ClrNames.AsyncCallback, ClrNames.Object },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        public static object BeginGetRequestStream(object webRequest, object callback, object state, int opCode, int mdToken, long moduleVersionPtr)
        {
            const string methodName = nameof(BeginGetRequestStream);

            Func<object, object, object, object> callBeginGetRequestStream;

            try
            {
                var instrumentedType = webRequest.GetInstrumentedType(WebRequestTypeName);
                callBeginGetRequestStream =
                    MethodBuilder<Func<object, object, object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(instrumentedType)
                       .WithParameters(typeof(AsyncCallback), typeof(object))
                       .WithNamespaceAndNameFilters(ClrNames.IAsyncResult, ClrNames.AsyncCallback, ClrNames.Object)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: WebRequestTypeName,
                    methodName: methodName,
                    instanceType: webRequest.GetType().AssemblyQualifiedName);
                throw;
            }

            var request = (WebRequest)webRequest;

            if (!(request is HttpWebRequest) || !IsTracingEnabled(request))
            {
                return callBeginGetRequestStream(webRequest, callback, state);
            }

            InjectHeadersForGetRequestStream(request);

            return callBeginGetRequestStream(webRequest, callback, state);
        }

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetResponse"/>.
        /// </summary>
        /// <param name="webRequest">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System", // .NET Framework
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { "System.Net.WebResponse" },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssembly = "System.Net.Requests", // .NET Core
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { "System.Net.WebResponse" },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        public static object GetResponse(object webRequest, int opCode, int mdToken, long moduleVersionPtr)
        {
            if (webRequest == null)
            {
                throw new ArgumentNullException(nameof(webRequest));
            }

            const string methodName = nameof(GetResponse);

            Func<object, WebResponse> callGetResponse;

            try
            {
                var instrumentedType = webRequest.GetInstrumentedType(WebRequestTypeName);
                callGetResponse =
                    MethodBuilder<Func<object, WebResponse>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(instrumentedType)
                       .WithNamespaceAndNameFilters("System.Net.WebResponse")
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: WebRequestTypeName,
                    methodName: methodName,
                    instanceType: webRequest.GetType().AssemblyQualifiedName);
                throw;
            }

            var request = (WebRequest)webRequest;

            if (!(request is HttpWebRequest) || !IsTracingEnabled(request))
            {
                return callGetResponse(webRequest);
            }

            // Check if any headers were injected by a previous call to GetRequestStream
            var spanContext = SpanContextPropagator.Instance.Extract(request.Headers.Wrap());

            // If this operation creates the trace, then we need to re-apply the sampling priority
            bool setSamplingPriority = spanContext?.SamplingPriority != null && Tracer.Instance.ActiveScope == null;

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, request.Method, request.RequestUri, IntegrationId, out var tags, spanContext?.TraceId, spanContext?.SpanId))
            {
                try
                {
                    if (scope != null)
                    {
                        if (setSamplingPriority)
                        {
                            scope.Span.SetTraceSamplingPriority(spanContext.SamplingPriority.Value);
                            scope.Span.Context.TraceContext.LockSamplingPriority();
                        }

                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, request.Headers.Wrap());
                    }

                    WebResponse response = callGetResponse(webRequest);

                    if (scope != null && response is HttpWebResponse webResponse)
                    {
                        scope.Span.SetHttpStatusCode((int)webResponse.StatusCode, isServer: false);
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetResponseAsync"/>.
        /// </summary>
        /// <param name="webRequest">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System", // .NET Framework
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Net.WebResponse>" },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssembly = "System.Net.Requests", // .NET Core
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Net.WebResponse>" },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        public static object GetResponseAsync(object webRequest, int opCode, int mdToken, long moduleVersionPtr)
        {
            const string methodName = nameof(GetResponseAsync);
            Func<object, Task<WebResponse>> callGetResponseAsync;

            try
            {
                var instrumentedType = webRequest.GetInstrumentedType(WebRequestTypeName);
                callGetResponseAsync =
                    MethodBuilder<Func<object, Task<WebResponse>>>
                        .Start(moduleVersionPtr, mdToken, opCode, methodName)
                        .WithConcreteType(instrumentedType)
                        .WithNamespaceAndNameFilters("System.Threading.Tasks.Task`1<System.Net.WebResponse>")
                        .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: WebRequestTypeName,
                    methodName: methodName,
                    instanceType: webRequest.GetType().AssemblyQualifiedName);
                throw;
            }

            return GetResponseAsyncInternal((WebRequest)webRequest, callGetResponseAsync);
        }

        private static async Task<WebResponse> GetResponseAsyncInternal(WebRequest webRequest, Func<object, Task<WebResponse>> originalMethod)
        {
            if (!(webRequest is HttpWebRequest) || !IsTracingEnabled(webRequest))
            {
                return await originalMethod(webRequest).ConfigureAwait(false);
            }

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, webRequest.Method, webRequest.RequestUri, IntegrationId, out var tags))
            {
                try
                {
                    if (scope != null)
                    {
                        // add distributed tracing headers to the HTTP request
                        SpanContextPropagator.Instance.Inject(scope.Span.Context, webRequest.Headers.Wrap());
                    }

                    WebResponse response = await originalMethod(webRequest).ConfigureAwait(false);

                    if (scope != null && response is HttpWebResponse webResponse)
                    {
                        scope.Span.SetHttpStatusCode((int)webResponse.StatusCode, isServer: false);
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static bool IsTracingEnabled(WebRequest request)
        {
            // check if tracing is disabled for this request via http header
            string value = request.Headers[HttpHeaderNames.TracingEnabled];
            return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }

        private static void InjectHeadersForGetRequestStream(WebRequest request)
        {
            var tracer = Tracer.Instance;

            if (tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                var span = ScopeFactory.CreateInactiveOutboundHttpSpan(tracer, request.Method, request.RequestUri, IntegrationId, out _, traceId: null, spanId: null, startTime: null, addToTraceContext: false);

                if (span?.Context != null)
                {
                    // Add distributed tracing headers to the HTTP request.
                    // The expected sequence of calls is GetRequestStream -> GetResponse. Headers can't be modified after calling GetRequestStream.
                    // At the same time, we don't want to set an active scope now, because it's possible that GetResponse will never be called.
                    // Instead, we generate a spancontext and inject it in the headers. GetResponse will fetch them and create an active scope with the right id.
                    SpanContextPropagator.Instance.Inject(span.Context, request.Headers.Wrap());
                }
            }
        }
    }
}
