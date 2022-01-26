// <copyright file="HostingApplication_ProcessRequestAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    /// <summary>
    /// System.Web.Http.ExceptionHandling.ExceptionHandlerExtensions calltarget instrumentation
    /// This instrumentation is based off the ASP.NET Web API 2 error handling design that is documented here:
    /// https://docs.microsoft.com/en-us/aspnet/web-api/overview/error-handling/web-api-global-error-handling
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.AspNetCore.Hosting",
        TypeName = "Microsoft.AspNetCore.Hosting.Internal.HostingApplication",
        MethodName = "ProcessRequestAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Context" },
        MinimumVersion = Major2,
        MaximumVersion = Major2,
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class HostingApplication_ProcessRequestAsync_Integration
    {
        private const string Major2 = "2";
        private const string IntegrationName = nameof(IntegrationId.AspNetCore);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.AspNetCore;
        private const string HttpRequestInOperationName = "aspnet_core.request";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HostingApplication_ProcessRequestAsync_Integration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the request context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">The request context</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context)
            where TContext : IContext
        {
            var tracer = Tracer.Instance;
            var security = Security.Instance;

            var shouldTrace = tracer.Settings.IsIntegrationEnabled(IntegrationId);
            var shouldSecure = security.Settings.Enabled;

            if (!shouldTrace && !shouldSecure)
            {
                return CallTargetState.GetDefault();
            }

            // First let's just make sure we get here
            IHttpContext httpContext = context.HttpContext;
            IHttpRequest request = httpContext.Request;
            Scope scope = null;

            if (shouldTrace)
            {
                // string host = request.Host.Value;
                string host = request.Host.Value;
                string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
                string url = !string.IsNullOrEmpty(host) ? $"{request.Scheme}://{host}{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}"
                                // HTTP 1.0 requests are not required to provide a Host to be valid
                                // Since this is just for display, we can provide a string that is
                                // not an actual Uri with only the fields that are specified.
                                // request.GetDisplayUrl(), used above, will throw an exception
                                // if request.Host is null.
                                : $"{request.Scheme}://{HttpRequestExtensions.NoHostSpecified}{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}";

                string absolutePath = request.PathBase.HasValue
                          ? request.PathBase.ToUriComponent() + request.Path.ToUriComponent()
                          : request.Path.ToUriComponent();

                string resourceUrl = UriHelpers.GetCleanUriPath(absolutePath)
                                               .ToLowerInvariant();
                string resourceName = $"{httpMethod} {resourceUrl}";

                // SpanContext propagatedContext = ExtractPropagatedContext(request);
                SpanContext propagatedContext = null;
                try
                {
                    // extract propagation details from http headers
                    var requestHeaders = request.Headers;

                    if (requestHeaders != null)
                    {
                        propagatedContext = SpanContextPropagator.Instance.Extract(new IHeaderDictionaryHeadersCollection(requestHeaders));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated HTTP headers.");
                }

                // var tagsFromHeaders = ExtractHeaderTags(request, tracer);
                var tagsFromHeaders = Enumerable.Empty<KeyValuePair<string, string>>();
                var settings = tracer.Settings;

                if (!settings.HeaderTags.IsNullOrEmpty())
                {
                    try
                    {
                        // extract propagation details from http headers
                        var requestHeaders = request.Headers;

                        if (requestHeaders != null)
                        {
                            tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(new IHeaderDictionaryHeadersCollection(requestHeaders), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                AspNetCoreTags tags;

                if (tracer.Settings.RouteTemplateResourceNamesEnabled)
                {
                    // var originalPath = request.PathBase.HasValue ? request.PathBase.Add(request.Path) : request.Path;
                    // httpContext.Features.Set(new RequestTrackingFeature(originalPath));
                    tags = new AspNetCoreEndpointTags();
                }
                else
                {
                    tags = new AspNetCoreTags();
                }

                scope = tracer.StartActiveInternal(HttpRequestInOperationName, propagatedContext, tags: tags);

                scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, tags, tagsFromHeaders);

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
            }

            /*

            if (shouldSecure)
            {
                security.InstrumentationGateway.RaiseRequestStart(httpContext, request, span, null);
                httpContext.Response.OnStarting(() =>
                {
                    // we subscribe here because in OnHostingHttpRequestInStop or HostingEndRequest it's too late,
                    // the waf is already disposed by the registerfordispose callback
                    security.InstrumentationGateway.RaiseRequestEnd(httpContext, request, span);
                    return System.Threading.Tasks.Task.CompletedTask;
                });

                httpContext.Response.OnCompleted(() =>
                {
                    security.InstrumentationGateway.RaiseLastChanceToWriteTags(httpContext, span);
                    return System.Threading.Tasks.Task.CompletedTask;
                });
            }
            */

            return new CallTargetState(scope, context.HttpContext);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="responseMessage">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse responseMessage, Exception exception, in CallTargetState state)
        {
            var scope = state.Scope;

            if (scope is not null)
            {
                var tracer = Tracer.Instance;
                var span = scope.Span;

                // we may need to update the resource name if none of the routing/mvc events updated it
                // if we had an unhandled exception, the status code is already updated
                if (state.State is IHttpContext httpContext && (string.IsNullOrEmpty(span.ResourceName) || !span.Error))
                {
                    /*
                    var httpRequest = arg.DuckCast<HttpRequestInStopStruct>();
                    HttpContext httpContext = httpRequest.HttpContext;
                    if (string.IsNullOrEmpty(span.ResourceName))
                    {
                        span.ResourceName = AspNetCoreRequestHandler.GetDefaultResourceName(httpContext.Request);
                    }
                    */

                    if (!span.Error)
                    {
                        span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, tracer.Settings);
                        span.SetHeaderTags(new IHeaderDictionaryHeadersCollection(httpContext.Response.Headers), tracer.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                    }
                }

                scope.DisposeWithException(exception);
            }

            return responseMessage;
        }
    }
}
#endif
