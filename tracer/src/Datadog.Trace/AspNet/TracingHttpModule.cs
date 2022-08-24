// <copyright file="TracingHttpModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.AspNet
{
    /// <summary>
    ///     IHttpModule used to trace within an ASP.NET HttpApplication request
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class TracingHttpModule : IHttpModule
    {
        internal static readonly IntegrationId IntegrationId = IntegrationId.AspNet;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TracingHttpModule));

        private static bool _canReadHttpResponseHeaders = true;

        private readonly string _httpContextScopeKey;
        private readonly string _requestOperationName;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TracingHttpModule" /> class.
        /// </summary>
        public TracingHttpModule()
            : this("aspnet.request")
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TracingHttpModule" /> class.
        /// </summary>
        /// <param name="operationName">The operation name to be used for the trace/span data generated</param>
        public TracingHttpModule(string operationName)
        {
            if (operationName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(operationName));
            }

            _requestOperationName = operationName;
            _httpContextScopeKey = string.Concat("__Datadog.Trace.AspNet.TracingHttpModule-", _requestOperationName);
        }

        /// <inheritdoc />
        public void Init(HttpApplication httpApplication)
        {
            httpApplication.BeginRequest += OnBeginRequest;
            httpApplication.EndRequest += OnEndRequest;
            httpApplication.Error += OnError;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        private void OnBeginRequest(object sender, EventArgs eventArgs)
        {
            Scope scope = null;
            try
            {
                var tracer = Tracer.Instance;

                if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled
                    return;
                }

                var httpContext = (sender as HttpApplication)?.Context;

                if (httpContext == null)
                {
                    return;
                }

                // Make sure the request wasn't already handled by another TracingHttpModule,
                // in case they're registered multiple times
                if (httpContext.Items.Contains(_httpContextScopeKey))
                {
                    return;
                }

                HttpRequest httpRequest = httpContext.Request;
                SpanContext propagatedContext = null;
                var tagsFromHeaders = Enumerable.Empty<KeyValuePair<string, string>>();

                if (tracer.InternalActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        var headers = httpRequest.Headers.Wrap();
                        propagatedContext = SpanContextPropagator.Instance.Extract(headers);
                        tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headers, tracer.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                string host = httpRequest.Headers.Get("Host");
                var userAgent = httpRequest.Headers.Get(HttpHeaderNames.UserAgent);
                string httpMethod = httpRequest.HttpMethod.ToUpperInvariant();
                string url = httpContext.Request.GetUrl(tracer.TracerManager.QueryStringManager);
                var tags = new WebTags();
                scope = tracer.StartActiveInternal(_requestOperationName, propagatedContext, tags: tags);
                // Leave resourceName blank for now - we'll update it in OnEndRequest
                scope.Span.DecorateWebServerSpan(resourceName: null, httpMethod, host, url, userAgent, tags, tagsFromHeaders);
                if (!tracer.Settings.IpHeaderDisabled)
                {
                    Headers.Ip.RequestIpExtractor.AddIpToTags(httpRequest.UserHostAddress, httpRequest.IsSecureConnection, key => httpRequest.Headers[key], tracer.Settings.IpHeader, tags);
                }

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);

                // Decorate the incoming HTTP Request with distributed tracing headers
                // in case the next processor cannot access the stored Scope
                // (e.g. WCF being hosted in IIS)
                if (HttpRuntime.UsingIntegratedPipeline)
                {
                    SpanContextPropagator.Instance.Inject(scope.Span.Context, httpRequest.Headers.Wrap());
                }

                httpContext.Items[_httpContextScopeKey] = scope;

                var security = Security.Instance;
                if (security.Settings.Enabled)
                {
                    if (httpRequest.ContentType?.IndexOf("application/x-www-form-urlencoded", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var formData = new Dictionary<string, object>();
                        foreach (string key in httpRequest.Form.Keys)
                        {
                            formData.Add(key, httpRequest.Form[key]);
                        }

                        security.InstrumentationGateway.RaiseBodyAvailable(httpContext, scope.Span, formData);
                    }
                }

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                // Dispose here, as the scope won't be in context items and won't get disposed on request end in that case...
                scope?.Dispose();
                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            try
            {
                if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled
                    return;
                }

                if (sender is HttpApplication app &&
                    app.Context.Items[_httpContextScopeKey] is Scope scope)
                {
                    try
                    {
                        // HttpServerUtility.TransferRequest presents an issue: The IIS request pipeline is run a second time
                        // from the same incoming HTTP request, but the HttpContext and HttpRequest objects from the two pipeline
                        // requests are completely isolated. Fortunately, the second request (somehow) maintains the original
                        // ExecutionContext, so the parent-child relationship between the two aspnet.request spans are correct.
                        //
                        // Since the EndRequest event will fire first for the second request, and this represents the HTTP response
                        // seen by end-users of the site, we'll only set HTTP tags on the root span and current span (if different)
                        // once with the information from the corresponding HTTP response.
                        // When this code is invoked again for the original HTTP request the HTTP tags must not be modified.
                        //
                        // Note: HttpServerUtility.TransferRequest cannot be invoked more than once, so we'll have at most two nested (in-process)
                        // aspnet.request spans at any given time: https://referencesource.microsoft.com/#System.Web/Hosting/IIS7WorkerRequest.cs,2400
                        var rootScope = scope.Root;
                        var rootSpan = rootScope.Span;

                        if (!rootSpan.HasHttpStatusCode())
                        {
                            rootSpan.SetHttpStatusCode(app.Context.Response.StatusCode, isServer: true, Tracer.Instance.Settings);
                            AddHeaderTagsFromHttpResponse(app.Context, rootScope);

                            if (scope.Span != rootSpan)
                            {
                                scope.Span.SetHttpStatusCode(app.Context.Response.StatusCode, isServer: true, Tracer.Instance.Settings);
                                AddHeaderTagsFromHttpResponse(app.Context, scope);
                            }
                        }

                        if (app.Context.Items[SharedItems.HttpContextPropagatedResourceNameKey] is string resourceName
                         && !string.IsNullOrEmpty(resourceName))
                        {
                            scope.Span.ResourceName = resourceName;
                        }
                        else
                        {
                            string path = UriHelpers.GetCleanUriPath(app.Request.Url, app.Request.ApplicationPath);
                            scope.Span.ResourceName = $"{app.Request.HttpMethod.ToUpperInvariant()} {path.ToLowerInvariant()}";
                        }

                        var security = Security.Instance;
                        if (security.Settings.Enabled)
                        {
                            var httpContext = (sender as HttpApplication)?.Context;

                            if (httpContext == null)
                            {
                                return;
                            }

                            // raise path params here for webforms cause there's no other hookpoint for path params, but for mvc/webapi, there's better hookpoint which only gives route params (and not {controller} and {actions} ones) so don't take precedence
                            security.InstrumentationGateway.RaisePathParamsAvailable(httpContext, scope.Span, httpContext.Request.RequestContext.RouteData.Values, eraseExistingAddress: false);
                            security.InstrumentationGateway.RaiseRequestStart(httpContext, httpContext.Request, scope.Span);
                            security.InstrumentationGateway.RaiseLastChanceToWriteTags(httpContext, scope.Span);
                        }

                        scope.Dispose();
                    }
                    finally
                    {
                        // Clear the context to make sure another TracingHttpModule doesn't try to close the same scope
                        TryClearContext(app.Context);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }

        private void OnError(object sender, EventArgs eventArgs)
        {
            try
            {
                var tracer = Tracer.Instance;

                if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled
                    return;
                }

                var httpContext = (sender as HttpApplication)?.Context;
                var exception = httpContext?.Error;

                // We want to ignore 404 exceptions here, as they are not errors
                var httpException = exception as HttpException;
                var is404 = httpException?.GetHttpCode() == 404;

                if (httpContext?.Items[_httpContextScopeKey] is Scope scope)
                {
                    AddHeaderTagsFromHttpResponse(httpContext, scope);

                    if (exception != null && !is404)
                    {
                        scope.Span.SetException(exception);
                        if (!HttpRuntime.UsingIntegratedPipeline)
                        {
                            // in classic mode, the exception won't cause the correct status code to be set
                            // even though a 500 response will be sent ultimately, so set it manually
                            scope.Span.SetHttpStatusCode(500, isServer: true, tracer.Settings);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }

        private void TryClearContext(HttpContext context)
        {
            try
            {
                context.Items.Remove(_httpContextScopeKey);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while clearing the HttpContext");
            }
        }

        private void AddHeaderTagsFromHttpResponse(System.Web.HttpContext httpContext, Scope scope)
        {
            if (httpContext != null && HttpRuntime.UsingIntegratedPipeline && _canReadHttpResponseHeaders && !Tracer.Instance.Settings.HeaderTags.IsNullOrEmpty())
            {
                try
                {
                    scope.Span.SetHeaderTags(httpContext.Response.Headers.Wrap(), Tracer.Instance.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                }
                catch (PlatformNotSupportedException ex)
                {
                    // Despite the HttpRuntime.UsingIntegratedPipeline check, we can still fail to access response headers, for example when using Sitefinity: "This operation requires IIS integrated pipeline mode"
                    Log.Error(ex, "Unable to access response headers when creating header tags. Disabling for the rest of the application lifetime.");
                    _canReadHttpResponseHeaders = false;
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
