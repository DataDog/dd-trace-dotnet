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

        internal static void AddHeaderTagsFromHttpResponse(HttpContext httpContext, Scope scope)
        {
            if (!Tracer.Instance.Settings.HeaderTagsInternal.IsNullOrEmpty() &&
                httpContext != null &&
                HttpRuntime.UsingIntegratedPipeline &&
                _canReadHttpResponseHeaders)
            {
                try
                {
                    scope.Span.SetHeaderTags(httpContext.Response.Headers.Wrap(), Tracer.Instance.Settings.HeaderTagsInternal, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
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
            bool shouldDisposeScope = true;
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
                var requestHeaders = RequestDataHelper.GetHeaders(httpRequest) ?? new System.Collections.Specialized.NameValueCollection();
                NameValueHeadersCollection? headers = null;
                SpanContext propagatedContext = null;
                if (tracer.InternalActiveScope == null)
                {
                    try
                    {
                        // extract propagated http headers
                        headers = requestHeaders.Wrap();
                        propagatedContext = SpanContextPropagator.Instance.Extract(headers.Value);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated HTTP headers.");
                    }
                }

                string host = requestHeaders.Get("Host");
                var userAgent = requestHeaders.Get(HttpHeaderNames.UserAgent);
                string httpMethod = httpRequest.HttpMethod.ToUpperInvariant();
                string url = httpContext.Request.GetUrlForSpan(tracer.TracerManager.QueryStringManager);
                var tags = new WebTags();
                scope = tracer.StartActiveInternal(_requestOperationName, propagatedContext, tags: tags);
                // Leave resourceName blank for now - we'll update it in OnEndRequest
                scope.Span.DecorateWebServerSpan(resourceName: null, httpMethod, host, url, userAgent, tags);
                if (headers is not null)
                {
                    SpanContextPropagator.Instance.AddHeadersToSpanAsTags(scope.Span, headers.Value, tracer.Settings.HeaderTagsInternal, defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                }

                if (tracer.Settings.IpHeaderEnabled || Security.Instance.Enabled)
                {
                    Headers.Ip.RequestIpExtractor.AddIpToTags(httpRequest.UserHostAddress, httpRequest.IsSecureConnection, key => requestHeaders[key], tracer.Settings.IpHeader, tags);
                }

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);

                // Decorate the incoming HTTP Request with distributed tracing headers
                // in case the next processor cannot access the stored Scope
                // (e.g. WCF being hosted in IIS)
                if (HttpRuntime.UsingIntegratedPipeline)
                {
                    SpanContextPropagator.Instance.Inject(scope.Span.Context, requestHeaders.Wrap());
                }

                httpContext.Items[_httpContextScopeKey] = scope;
                shouldDisposeScope = false;

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

                var security = Security.Instance;
                if (security.Enabled)
                {
                    SecurityCoordinator.ReportWafInitInfoOnce(security, scope.Span);
                    var securityCoordinator = new SecurityCoordinator(security, scope.Span);

                    // request args
                    var args = securityCoordinator.GetBasicRequestArgsForWaf();

                    // body args
                    if (httpRequest.ContentType?.IndexOf("application/x-www-form-urlencoded", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        var bodyArgs = securityCoordinator.GetBodyFromRequest();
                        args.Add(AddressesConstants.RequestBody, bodyArgs);
                    }

                    securityCoordinator.BlockAndReport(args);
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
                }

                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            var securityContextCleaned = false;

            try
            {
                var tracer = Tracer.Instance;
                if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
                {
                    // integration disabled
                    return;
                }

                if (sender is HttpApplication app &&
                    app.Context.Items[_httpContextScopeKey] is Scope scope)
                {
                    try
                    {
                        var rootScope = scope.Root;
                        var rootSpan = rootScope.Span;

                        // the security needs to come before collecting the response code,
                        // since blocking will change the response code
                        var security = Security.Instance;
                        if (security.Enabled)
                        {
                            var securityCoordinator = new SecurityCoordinator(security, rootSpan);
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

                            securityCoordinator.BlockAndReport(args, true);

                            securityCoordinator.AddResponseHeadersToSpanAndCleanup();
                            securityContextCleaned = true;
                        }

                        if (Iast.Iast.Instance.Settings.Enabled && IastModule.AddRequestVulnerabilitiesAllowed())
                        {
                            if (rootSpan is not null && HttpRuntime.UsingIntegratedPipeline && _canReadHttpResponseHeaders)
                            {
                                try
                                {
                                    var requestUrl = RequestDataHelper.GetUrl(app.Context.Request);
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
                        if (!rootSpan.HasHttpStatusCode())
                        {
                            var response = app.Context.Response;
                            var status = response.StatusCode;
                            if (status == 500 && !response.IsClientConnected && app.Context.Error is null)
                            {
                                // rare case when client disconnects - IIS returns a 400 but reports a 500
                                status = 400;
                            }

                            rootSpan.SetHttpStatusCode(status, isServer: true, Tracer.Instance.Settings);
                            AddHeaderTagsFromHttpResponse(app.Context, rootScope);

                            if (scope.Span != rootSpan)
                            {
                                scope.Span.SetHttpStatusCode(status, isServer: true, Tracer.Instance.Settings);
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
                            var url = RequestDataHelper.GetUrl(app.Request);
                            if (url is not null)
                            {
                                string path = UriHelpers.GetCleanUriPath(url, app.Request.ApplicationPath);
                                scope.Span.ResourceName = $"{app.Request.HttpMethod.ToUpperInvariant()} {path.ToLowerInvariant()}";
                            }
                            else
                            {
                                scope.Span.ResourceName = $"{app.Request.HttpMethod.ToUpperInvariant()}";
                            }
                        }
                    }
                    finally
                    {
                        scope.Dispose();
                        // Clear the context to make sure another TracingHttpModule doesn't try to close the same scope
                        TryClearContext(app.Context);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Datadog ASP.NET HttpModule instrumentation error");
            }
            finally
            {
                // security might have been disabled in the meantime but contexts would still be open
                // and this integration may be disabled but others might have opened a context
                if (!securityContextCleaned && sender is HttpApplication app)
                {
                    var securityTransport = new SecurityCoordinator.HttpTransport(app.Context);
                    securityTransport.DisposeAdditiveContext();
                }
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

                    if (exception != null && !is404 && exception is not AppSec.BlockException)
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
    }
}

#endif
