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
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
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
    public sealed class TracingHttpModule : IHttpModule
    {
        internal static readonly IntegrationId IntegrationId = IntegrationId.AspNet;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TracingHttpModule));

        private static bool _canReadHttpResponseHeaders = true;

        private readonly string _httpContextScopeKey;

        /// <summary>
        /// This key is only present when inferred proxy spans are enabled AND necessary proxy headers were present.
        /// </summary>
        /// <see cref="ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled"/>
        private readonly string _httpContextProxyScopeKey;
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
            _httpContextProxyScopeKey = string.Concat(_httpContextScopeKey, ".proxy");
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

        private static string BuildResourceName(Tracer tracer, HttpRequest httpRequest)
        {
            var url = tracer.Settings.BypassHttpRequestUrlCachingEnabled
                               ? RequestDataHelper.BuildUrl(httpRequest)
                               : RequestDataHelper.GetUrl(httpRequest);
            if (url is not null)
            {
                var path = UriHelpers.GetCleanUriPath(url, httpRequest.ApplicationPath);
                return $"{httpRequest.HttpMethod.ToUpperInvariant()} {path.ToLowerInvariant()}";
            }
            else
            {
                return $"{httpRequest.HttpMethod.ToUpperInvariant()}";
            }
        }

        internal static void AddHeaderTagsFromHttpResponse(HttpContext httpContext, Scope scope)
        {
            if (!Tracer.Instance.CurrentTraceSettings.Settings.HeaderTags.IsNullOrEmpty() &&
                httpContext != null &&
                HttpRuntime.UsingIntegratedPipeline &&
                _canReadHttpResponseHeaders)
            {
                try
                {
                    scope.Span.SetHeaderTags(httpContext.Response.Headers.Wrap(), Tracer.Instance.CurrentTraceSettings.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
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

        private void OnBeginRequest(object sender, EventArgs eventArgs)
        {
            Scope scope = null;
            Scope inferredProxyScope = null;
            bool shouldDisposeScope = true;
            try
            {
                var tracer = Tracer.Instance;

                if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
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
                var requestHeaders = RequestDataHelper.GetHeaders(httpRequest) ?? [];
                var headers = requestHeaders.Wrap();
                PropagationContext extractedContext = default;

                if (tracer.InternalActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headers).MergeBaggageInto(Baggage.Current);

                        // if inferred proxy spans are enabled, try to extract and create the proxy span
                        if (tracer.Settings.InferredProxySpansEnabled)
                        {
                            if (InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(tracer, headers, extractedContext) is { } proxyContext)
                            {
                                inferredProxyScope = proxyContext.Scope;
                                // updates the extracted context with the inferred proxy span context so that it has the inferred context
                                extractedContext = proxyContext.Context;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                string host = requestHeaders.Get("Host");
                var userAgent = requestHeaders.Get(HttpHeaderNames.UserAgent);
                string httpMethod = httpRequest.HttpMethod.ToUpperInvariant();
                var url = httpContext.Request.GetUrlForSpan(tracer.TracerManager.QueryStringManager, tracer.Settings.BypassHttpRequestUrlCachingEnabled);
                var tags = new WebTags();
                // FIXME: InstrumentationName should be added to InstrumentationTags
                tags.SetTag("component", "aspnet");
                scope = tracer.StartActiveInternal(_requestOperationName, extractedContext.SpanContext, tags: tags);
                // Attempt to set Resource Name to something that will be close to what is expected
                // Note: we will go and re-do it in OnEndRequest, but doing it here will allow for resource-based sampling
                // this likely won't be perfect - but we need something to try and allow resource-based sampling to function
                var resourceName = tracer.CurrentTraceSettings.HasResourceBasedSamplingRule
                                       ? BuildResourceName(tracer, httpRequest)
                                       : null;
                scope.Span.DecorateWebServerSpan(resourceName: resourceName, httpMethod, host, url, userAgent, tags);
                tracer.TracerManager.SpanContextPropagator.AddHeadersToSpanAsTags(scope.Span, headers, tracer.CurrentTraceSettings.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);

                tracer.TracerManager.SpanContextPropagator.AddBaggageToSpanAsTags(scope.Span, extractedContext.Baggage, tracer.Settings.BaggageTagKeys);

                if (tracer.Settings.IpHeaderEnabled || Security.Instance.AppsecEnabled)
                {
                    Headers.Ip.RequestIpExtractor.AddIpToTags(httpRequest.UserHostAddress, httpRequest.IsSecureConnection, key => requestHeaders[key], tracer.Settings.IpHeader, tags);
                }

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: true);

                // Decorate the incoming HTTP Request with distributed tracing headers
                // in case the next processor cannot access the stored Scope
                // (e.g. WCF being hosted in IIS)
                if (HttpRuntime.UsingIntegratedPipeline)
                {
                    var injectedContext = new PropagationContext(scope.Span.Context, Baggage.Current);
                    tracer.TracerManager.SpanContextPropagator.Inject(injectedContext, requestHeaders.Wrap());
                }

                if (inferredProxyScope is not null)
                {
                    httpContext.Items[_httpContextProxyScopeKey] = inferredProxyScope;
                }

                httpContext.Items[_httpContextScopeKey] = scope;
                shouldDisposeScope = false;

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

                var security = Security.Instance;
                if (security.AppsecEnabled)
                {
                    var securityCoordinator = SecurityCoordinator.Get(security, scope.Span, httpContext);
                    securityCoordinator.Reporter.ReportWafInitInfoOnce(security.WafInitResult);

                    // request args
                    var args = securityCoordinator.GetBasicRequestArgsForWaf();

                    // body args
                    if (httpRequest.ContentType?.IndexOf("application/x-www-form-urlencoded", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var bodyArgs = securityCoordinator.GetBodyFromRequest();
                        if (bodyArgs is not null)
                        {
                            args.Add(AddressesConstants.RequestBody, bodyArgs);
                        }
                    }

                    securityCoordinator.BlockAndReport(args, isInHttpTracingModule: true);
                }

                var iastInstance = Iast.Iast.Instance;
                if (iastInstance.Settings.Enabled && iastInstance.OverheadController.AcquireRequest())
                {
                    var traceContext = scope.Span?.Context?.TraceContext;
                    traceContext?.EnableIastInRequest();
                    traceContext?.IastRequestContext?.AddRequestData(httpRequest);
                }
            }
            catch (Exception ex)
            {
                if (shouldDisposeScope)
                {
                    // Dispose here, as the scope won't be in context items and won't get disposed on request end in that case...
                    scope?.Dispose();
                    inferredProxyScope?.Dispose();
                }

                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            try
            {
                var tracer = Tracer.Instance;
                var settings = tracer.CurrentTraceSettings.Settings;
                if (!settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled
                    return;
                }

                if (sender is HttpApplication app &&
                    app.Context.Items[_httpContextScopeKey] is Scope scope)
                {
                    var proxyScope = app.Context.Items[_httpContextProxyScopeKey] as Scope;

                    try
                    {
                        var currentSpan = scope.Span;
                        var rootScope = scope.Root;
                        var rootSpan = rootScope.Span;

                        // the security needs to come before collecting the response code,
                        // since blocking will change the response code
                        var security = Security.Instance;
                        if (security.AppsecEnabled)
                        {
                            var securityCoordinator = SecurityCoordinator.Get(security, rootSpan, app.Context);
                            var args = securityCoordinator.GetBasicRequestArgsForWaf();
                            args.Add(AddressesConstants.RequestPathParams, securityCoordinator.GetPathParams());

                            if (HttpRuntime.UsingIntegratedPipeline && _canReadHttpResponseHeaders)
                            {
                                // path params here for webforms cause there's no other hookpoint for path params, but for mvc/webapi, there's better hookpoint which only gives route params (and not {controller} and {actions} ones) so don't take precedence
                                try
                                {
                                    args.Add(AddressesConstants.ResponseHeaderNoCookies, securityCoordinator.GetResponseHeadersForWaf());
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

                            securityCoordinator.BlockAndReport(args, true, isInHttpTracingModule: true);

                            securityCoordinator.Reporter.AddResponseHeadersToSpan();
                        }

                        if (Iast.Iast.Instance.Settings.Enabled && IastModule.AddRequestVulnerabilitiesAllowed())
                        {
                            if (rootSpan is not null && HttpRuntime.UsingIntegratedPipeline && _canReadHttpResponseHeaders)
                            {
                                try
                                {
                                    var requestUrl = tracer.Settings.BypassHttpRequestUrlCachingEnabled
                                        ? RequestDataHelper.BuildUrl(app.Context.Request)
                                        : RequestDataHelper.GetUrl(app.Context.Request);
                                    ReturnedHeadersAnalyzer.Analyze(app.Context.Response.Headers, IntegrationId, rootSpan.ServiceName, app.Context.Response.StatusCode, requestUrl?.Scheme);
                                    var headers = RequestDataHelper.GetHeaders(app.Context.Request);
                                    if (headers is not null)
                                    {
                                        InsecureAuthAnalyzer.AnalyzeInsecureAuth(headers, IntegrationId, app.Context.Response.StatusCode);
                                    }
                                }
                                catch (PlatformNotSupportedException ex)
                                {
                                    // Despite the HttpRuntime.UsingIntegratedPipeline check, we can still fail to access response headers, for example when using Sitefinity: "This operation requires IIS integrated pipeline mode"
                                    Log.Error(ex, "Unable to access response headers when analyzing headers. Disabling for the rest of the application lifetime.");
                                    _canReadHttpResponseHeaders = false;
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Error analyzing HTTP response headers");
                                }
                            }

                            CookieAnalyzer.AnalyzeCookies(app.Context.Response.Cookies, IntegrationId);
                        }

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

                        var response = app.Context.Response;
                        int status;

                        if (response.StatusCode == 500 && !response.IsClientConnected && app.Context.Error is null)
                        {
                            // rare case when client disconnects - IIS returns a 400 but reports a 500
                            status = 400;
                        }
                        else
                        {
                            status = response.StatusCode;
                        }

                        // add "http.status_code" tag to the root span
                        if (!rootSpan.HasHttpStatusCode())
                        {
                            rootSpan.SetHttpStatusCode(status, isServer: true, settings);
                            AddHeaderTagsFromHttpResponse(app.Context, rootScope);
                        }

                        // also add "http.status_code" tag to the current span if it's not the root
                        if (currentSpan != rootSpan && !currentSpan.HasHttpStatusCode())
                        {
                            currentSpan.SetHttpStatusCode(status, isServer: true, settings);
                            AddHeaderTagsFromHttpResponse(app.Context, scope);
                        }

                        // also add "http.status_code" tag to the inferred proxy span
                        if (proxyScope?.Span is { } proxySpan && proxySpan != rootSpan && !proxySpan.HasHttpStatusCode())
                        {
                            proxySpan.SetHttpStatusCode(status, isServer: true, settings);
                            AddHeaderTagsFromHttpResponse(app.Context, proxyScope);
                        }

                        if (app.Context.Items[SharedItems.HttpContextPropagatedResourceNameKey] is string resourceName
                         && !string.IsNullOrEmpty(resourceName))
                        {
                            currentSpan.ResourceName = resourceName;
                        }
                        else
                        {
                            currentSpan.ResourceName = BuildResourceName(tracer, app.Request);
                        }
                    }
                    finally
                    {
                        scope.Dispose();
                        proxyScope?.Dispose();
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

                var settings = tracer.CurrentTraceSettings.Settings;
                if (!settings.IsIntegrationEnabled(IntegrationId))
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
                    var proxyScope = httpContext?.Items[_httpContextProxyScopeKey] as Scope;
                    AddHeaderTagsFromHttpResponse(httpContext, scope);
                    if (proxyScope != null)
                    {
                        AddHeaderTagsFromHttpResponse(httpContext, proxyScope);
                    }

                    if (exception != null && !is404 && exception is not AppSec.BlockException)
                    {
                        scope.Span.SetException(exception);
                        proxyScope?.Span.SetException(exception);
                        if (!HttpRuntime.UsingIntegratedPipeline)
                        {
                            // in classic mode, the exception won't cause the correct status code to be set
                            // even though a 500 response will be sent ultimately, so set it manually
                            scope.Span.SetHttpStatusCode(500, isServer: true, settings);
                            proxyScope?.Span.SetHttpStatusCode(500, isServer: true, settings);
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
                context.Items.Remove(_httpContextProxyScopeKey);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while clearing the HttpContext");
            }
        }
    }
}

#endif
