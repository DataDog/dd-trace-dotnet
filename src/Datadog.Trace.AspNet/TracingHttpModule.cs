// <copyright file="TracingHttpModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Transport.Http;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.AspNet
{
    /// <summary>
    ///     IHttpModule used to trace within an ASP.NET HttpApplication request
    /// </summary>
    public class TracingHttpModule : IHttpModule
    {
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.AspNet));

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TracingHttpModule));

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
            _requestOperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));

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

                if (tracer.ActiveScope == null)
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
                string httpMethod = httpRequest.HttpMethod.ToUpperInvariant();
                string url = httpRequest.RawUrl.ToLowerInvariant();

                var tags = new WebTags();
                scope = tracer.StartActiveWithTags(_requestOperationName, propagatedContext, tags: tags);
                // Leave resourceName blank for now - we'll update it in OnEndRequest
                scope.Span.DecorateWebServerSpan(resourceName: null, httpMethod, host, url, tags, tagsFromHeaders);

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
                if (security.Enabled)
                {
                    RaiseIntrumentationEvent(security, httpContext, httpRequest, scope.Span);
                }
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
                        if (HttpRuntime.UsingIntegratedPipeline)
                        {
                            scope.Span.SetHeaderTags<IHeadersCollection>(app.Context.Response.Headers.Wrap(), Tracer.Instance.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                        }

                        scope.Span.SetHttpStatusCode(app.Context.Response.StatusCode, isServer: true);

                        if (app.Context.Items[SharedConstants.HttpContextPropagatedResourceNameKey] is string resourceName
                            && !string.IsNullOrEmpty(resourceName))
                        {
                            scope.Span.ResourceName = resourceName;
                        }
                        else
                        {
                            string path = UriHelpers.GetCleanUriPath(app.Request.Url);
                            scope.Span.ResourceName = $"{app.Request.HttpMethod.ToUpperInvariant()} {path.ToLowerInvariant()}";
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
                var httpContext = (sender as HttpApplication)?.Context;
                var exception = httpContext?.Error;

                // We want to ignore 404 exceptions here, as they are not errors
                var httpException = exception as HttpException;
                var is404 = httpException?.GetHttpCode() == 404;

                if (httpContext.Items[_httpContextScopeKey] is Scope scope)
                {
                    if (HttpRuntime.UsingIntegratedPipeline)
                    {
                        scope.Span.SetHeaderTags<IHeadersCollection>(httpContext.Response.Headers.Wrap(), Tracer.Instance.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);
                    }

                    if (exception != null && !is404)
                    {
                        scope.Span.SetException(exception);
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

        private void RaiseIntrumentationEvent(IDatadogSecurity security, HttpContext context, HttpRequest request, Span relatedSpan)
        {
            var dict = request.PrepareArgsForWaf();
            security.InstrumentationGateway.RaiseEvent(dict, new HttpTransport(context), relatedSpan);
        }
    }
}
