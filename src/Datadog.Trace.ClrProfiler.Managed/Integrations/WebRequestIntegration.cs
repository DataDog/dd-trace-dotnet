using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
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
        private const string IntegrationName = "WebRequest";
        private const string Major4 = "4";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(WebRequestIntegration));

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetResponse"/>.
        /// </summary>
        /// <param name="webRequest">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { "System", "System.Net.Requests" },
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { "System.Net.WebResponse" },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
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
                var instrumentedType = webRequest.GetInstrumentedType("System.Net.WebRequest");
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

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, request.Method, request.RequestUri, IntegrationName))
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
                        scope.Span.SetTag(Tags.HttpStatusCode, ((int)webResponse.StatusCode).ToString());
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
            TargetAssemblies = new[] { "System", "System.Net.Requests" },
            TargetType = WebRequestTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Net.WebResponse>" },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object GetResponseAsync(object webRequest, int opCode, int mdToken, long moduleVersionPtr)
        {
            const string methodName = nameof(GetResponseAsync);
            Func<object, Task<WebResponse>> callGetResponseAsync;

            try
            {
                var instrumentedType = webRequest.GetInstrumentedType("System.Net.WebRequest");
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

            using (var scope = ScopeFactory.CreateOutboundHttpScope(Tracer.Instance, webRequest.Method, webRequest.RequestUri, IntegrationName))
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
                        scope.Span.SetTag(Tags.HttpStatusCode, ((int)webResponse.StatusCode).ToString());
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
