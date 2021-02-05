using System;
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

            var tracer = Tracer.Instance;

            if (tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                var spanContext = ScopeFactory.CreateHttpSpanContext(tracer, IntegrationId);

                if (spanContext != null)
                {
                    // Add distributed tracing headers to the HTTP request.
                    // The expected sequence of calls is GetRequestStream -> GetResponse. Headers can't be modified after calling GetRequestStream.
                    // At the same time, we don't want to set an active scope now, because it's possible that GetResponse will never be called.
                    // Instead, we generate a spancontext and inject it in the headers. GetResponse will fetch them and create an active scope with the right id.
                    SpanContextPropagator.Instance.Inject(spanContext, request.Headers.Wrap());
                }
            }

            return callGetRequestStream(webRequest);
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

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, request.Method, request.RequestUri, IntegrationId, out var tags, spanContext?.SpanId))
            {
                try
                {
                    if (scope != null)
                    {
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
                        .WithNamespaceAndNameFilters(ClrNames.GenericTask)
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
    }
}
