#if NETFRAMEWORK
using System;
using System.Web;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    internal static class HttpContextHelpers
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HttpContextHelpers));
        private static bool CanReadHttpResponseHeaders = true;

        internal static void AddHeaderTagsFromHttpResponse(System.Web.HttpContext httpContext, Scope scope)
        {
            if (httpContext != null && HttpRuntime.UsingIntegratedPipeline && CanReadHttpResponseHeaders && !Tracer.Instance.Settings.HeaderTags.IsNullOrEmpty())
            {
                try
                {
                    scope.Span.SetHeaderTags<IHeadersCollection>(httpContext.Response.Headers.Wrap(), Tracer.Instance.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                }
                catch (PlatformNotSupportedException ex)
                {
                    // Despite the HttpRuntime.UsingIntegratedPipeline check, we can still fail to access response headers, for example when using Sitefinity: "This operation requires IIS integrated pipeline mode"
                    Log.Error(ex, "Unable to access response headers when creating header tags. Disabling for the rest of the application lifetime.");
                    CanReadHttpResponseHeaders = false;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting HTTP headers to create header tags.");
                }
            }
        }
    }
}
#endif
