using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracer integration for WebRequest.
    /// </summary>
    public static class WebRequestIntegration
    {
        private const string IntegrationName = "WebRequest";
        private const string Major4 = "4";

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetResponse"/>.
        /// </summary>
        /// <param name="webRequest">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System", // .NET Framework
            TargetType = "System.Net.WebRequest",
            TargetSignatureTypes = new string[] { null },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssembly = "System.Net.Requests", // .NET Core
            TargetType = "System.Net.WebRequest",
            TargetSignatureTypes = new string[] { null },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object GetResponse(object webRequest, int opCode)
        {
            var request = (WebRequest)webRequest;

            if (!(request is HttpWebRequest) || !IsTracingEnabled(request))
            {
                return request.GetResponse();
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

                    WebResponse response = request.GetResponse();

                    if (scope != null && response is HttpWebResponse webResponse)
                    {
                        scope.Span.SetTag(Tags.HttpStatusCode, ((int)webResponse.StatusCode).ToString());
                    }

                    return response;
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for <see cref="WebRequest.GetResponseAsync"/>.
        /// </summary>
        /// <param name="request">The <see cref="WebRequest"/> instance to instrument.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <returns>Returns the value returned by the inner method call.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Net",
            TargetType = "System.Net.WebRequest",
            TargetSignatureTypes = new string[] { null },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object GetResponseAsync(object request, int opCode)
        {
            return GetResponseAsyncInternal((WebRequest)request);
        }

        private static async Task<WebResponse> GetResponseAsyncInternal(WebRequest request)
        {
            if (!(request is HttpWebRequest) || !IsTracingEnabled(request))
            {
                return await request.GetResponseAsync().ConfigureAwait(false);
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

                    WebResponse response = await request.GetResponseAsync().ConfigureAwait(false);

                    if (scope != null && response is HttpWebResponse webResponse)
                    {
                        scope.Span.SetTag(Tags.HttpStatusCode, ((int)webResponse.StatusCode).ToString());
                    }

                    return response;
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
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
