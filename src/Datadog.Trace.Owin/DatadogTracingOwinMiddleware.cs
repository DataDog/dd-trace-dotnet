using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Microsoft.Owin;

namespace Datadog.Trace.Owin
{
    /// <summary>
    /// Datadog middleware that can be inserted into the OWIN middleware pipeline
    /// to generate traces
    /// </summary>
    public class DatadogTracingOwinMiddleware : OwinMiddleware
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(DatadogTracingOwinMiddleware));
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.Owin));

        private static Func<IHeaderDictionary, string, IEnumerable<string>> headersGetter = (carrier, key) =>
        {
            IList<string> values = carrier.GetCommaSeparatedValues(key);
            if (values != null)
            {
                return values;
            }
            else
            {
                return Enumerable.Empty<string>();
            }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="DatadogTracingOwinMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next OWIN middleware in the pipeline</param>
        public DatadogTracingOwinMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        /// <inheritdoc/>
        public override async Task Invoke(IOwinContext context)
        {
            Scope? scope = CreateScope(context);

            try
            {
                await Next.Invoke(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                scope?.Span.SetException(ex);
                throw;
            }
            finally
            {
                var statusCode = context.Response.StatusCode;
                scope?.Span.SetServerStatusCode(statusCode);

                scope?.Dispose();
            }
        }

        private static Scope? CreateScope(IOwinContext context)
        {
            Scope? scope = null;

            try
            {
                if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled, don't create a scope, skip this trace
                    return null;
                }

                var tracer = Tracer.Instance;

                IOwinRequest request = context.Request;
                string host = request.Host.Value;
                string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
                string url = GetUrl(request);

                string absolutePath = request.Path.Value;
                if (request.PathBase.HasValue)
                {
                    absolutePath = request.PathBase.Value + absolutePath;
                }

                string resourceUrl = UriHelpers.GetRelativeUrl(absolutePath, tryRemoveIds: true)
                               .ToLowerInvariant();

                string resourceName = $"{httpMethod} {resourceUrl}";

                SpanContext? propagatedContext = ExtractPropagatedContext(request);
                var tagsFromHeaders = ExtractHeaderTags(request, tracer);

                var tags = new OwinTags();
                scope = tracer.StartActiveWithTags("owin.request", propagatedContext, tags: tags);
                scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, tags, tagsFromHeaders);

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating scope.");
            }

            return scope;
        }

        private static string GetUrl(IOwinRequest request)
        {
            return $"{request.Scheme}://{request.Host.Value}{request.PathBase.Value}{request.Path.Value}";
        }

        private static SpanContext? ExtractPropagatedContext(IOwinRequest request)
        {
            try
            {
                // extract propagation details from http headers
                var requestHeaders = request.Headers;

                if (requestHeaders != null)
                {
                    return SpanContextPropagator.Instance.Extract(request.Headers, headersGetter);
                }
            }
            catch (Exception ex)
            {
                Log.SafeLogError(ex, "Error extracting propagated HTTP headers.");
            }

            return null;
        }

        private static IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags(IOwinRequest request, IDatadogTracer tracer)
        {
            var settings = tracer.Settings;

            if (!settings.HeaderTags.IsEmpty())
            {
                try
                {
                    // extract propagation details from http headers
                    var requestHeaders = request.Headers;

                    if (requestHeaders != null)
                    {
                        return SpanContextPropagator.Instance.ExtractHeaderTags(request.Headers, headersGetter, settings.HeaderTags);
                    }
                }
                catch (Exception ex)
                {
                    Log.SafeLogError(ex, "Error extracting propagated HTTP headers.");
                }
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}
