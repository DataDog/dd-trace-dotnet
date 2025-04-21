// <copyright file="ResolveAppIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

using AppFunc = System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Owin
{
    /// <summary>
    /// System.Net.Http.SocketsHttpHandler calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.Owin.Hosting",
        TypeName = "Microsoft.Owin.Hosting.Engine.HostingEngine",
        MethodName = "ResolveApp",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "Microsoft.Owin.Hosting.Engine.StartContext" },
        MinimumVersion = "4.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ResolveAppIntegration
    {
        private const IntegrationId IntegrationId = Configuration.IntegrationId.Owin;
        private const string IntegrationName = nameof(Configuration.IntegrationId.Owin);

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TStartContext">Type of the start context</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">StartContext instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TStartContext>(TTarget instance, TStartContext context)
            where TStartContext : IStartContext
        {
            // At this point, context.Startup(context.Builder) is called
            // context.Builder is a MicrosoftOwin.Builder.AppBuilder
            // OpenTelemetry usually just calls appBuilder.Use<DiagnosticsMiddleware>(), which is Owin.AppBuilderUseExtensions.Use<T>(this IAppBuilder app, params object[] args) => instance Microsoft.Owin.Builder.AppBuilder.Use(object middleware, params object[] args)

            // We can add the middleware to the pipeline here, but we will not get the environment data from this encapsulate middleware: https://github.com/aspnet/AspNetKatana/blob/86fa511a6c4a598b08cf13a62280783bf2ea472f/src/Microsoft.Owin.Hosting/Utilities/Encapsulate.cs
            // We don't need to add middleware as a class, we can add it as a delegate which will avoid any issues with type-checking: https://benfoster.io/blog/how-to-write-owin-middleware-in-5-different-steps/
            context.Builder.Use(new Func<AppFunc, AppFunc>(next => env => Invoke(next, env)), Array.Empty<object>());
            return CallTargetState.GetDefault();
        }

        private static async Task Invoke(AppFunc next, IDictionary<string, object> environment)
        {
            Scope scope = null;

            try
            {
                Console.WriteLine("Begin Request");
                var tracer = Tracer.Instance;

                var requestHeaders = environment["owin.RequestHeaders"] as IDictionary<string, string[]>;

                var host = requestHeaders["Host"]?.FirstOrDefault() as string;
                var httpMethod = (environment["owin.RequestMethod"] as string)?.ToUpperInvariant() ?? "UNKNOWN";
                // var url = UriHelpers.GetCleanUriPath(path).ToLowerInvariant();
                var path = environment["owin.RequestPath"] as string;
                var userAgent = requestHeaders["User-Agent"]?.FirstOrDefault() as string;

                var resourceName = $"{httpMethod} {path}";
                PropagationContext extractedContext = default;
                try
                {
                    // extract propagation details from http headers
                    if (requestHeaders is { } headers)
                    {
                        extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headers, (carrier, name) => carrier[name]).MergeBaggageInto(Baggage.Current);
                    }
                }
                catch (Exception)
                {
                    // _log.Error(ex, "Error extracting propagated HTTP headers.");
                }

                // InferredProxyScopePropagationContext? proxyContext = null;

                /*
                if (tracer.Settings.InferredProxySpansEnabled && request.Headers is { } headers)
                {
                    proxyContext = InferredProxySpanHelper.ExtractAndCreateInferredProxyScope(tracer, new HeadersCollectionAdapter(headers), extractedContext);
                    if (proxyContext != null)
                    {
                        extractedContext = proxyContext.Value.Context;
                    }
                }
                */

                var tags = new WebTags();

                scope = tracer.StartActiveInternal("owin.request", extractedContext.SpanContext, tags: tags, links: extractedContext.Links);
                // scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, userAgent, tags);
                // AddHeaderTagsToSpan(scope.Span, request, tracer);

                /*
                if (tracer.Settings.IpHeaderEnabled || security.AppsecEnabled)
                {
                    var peerIp = new Headers.Ip.IpInfo(httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Connection.RemotePort);
                    string GetRequestHeaderFromKey(string key) => request.Headers.TryGetValue(key, out var value) ? value : string.Empty;
                    Headers.Ip.RequestIpExtractor.AddIpToTags(peerIp, request.IsHttps, GetRequestHeaderFromKey, tracer.Settings.IpHeader, tags);
                }

                var iastInstance = Iast.Iast.Instance;
                if (iastInstance.Settings.Enabled && iastInstance.OverheadController.AcquireRequest())
                {
                    // If the overheadController disables the vulnerability detection for this request, we do not initialize the iast context of TraceContext
                    scope.Span.Context?.TraceContext?.EnableIastInRequest();
                }
                */

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: true);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
                await next.Invoke(environment).ConfigureAwait(false);

                OnRequestEnd(scope, environment, exception: null);
            }
            catch (Exception ex)
            {
                OnRequestEnd(scope, environment, ex);
            }

            Console.WriteLine("End Request");
        }

        /*
        private static void AddHeaderTagsToSpan(ISpan span, HttpRequest request, Tracer tracer)
        {
            var headerTagsInternal = tracer.Settings.HeaderTags;

            if (!headerTagsInternal.IsNullOrEmpty())
            {
                try
                {
                    // extract propagation details from http headers
                    if (request.Headers is { } requestHeaders)
                    {
                        tracer.TracerManager.SpanContextPropagator.AddHeadersToSpanAsTags(
                            span,
                            new HeadersCollectionAdapter(requestHeaders),
                            headerTagsInternal,
                            defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }
        }
        */

        private static void OnRequestEnd(Scope scope, IDictionary<string, object> environment, Exception exception)
        {
            if (scope is null)
            {
                return;
            }

            var span = scope.Span;
            // var isMissingHttpStatusCode = !span.HasHttpStatusCode();

            if (exception is not null)
            {
                scope.Span.SetException(exception);
            }

            // span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, tracer.Settings);

            // span.SetHeaderTags(new HeadersCollectionAdapter(httpContext.Response.Headers), tracer.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);

            /*
            if (security.AppsecEnabled)
            {
                var securityCoordinator = SecurityCoordinator.Get(security, span, new SecurityCoordinator.HttpTransport(httpContext));
                securityCoordinator.Reporter.AddResponseHeadersToSpan();
            }
            */

            scope.Dispose();
        }
    }
}
