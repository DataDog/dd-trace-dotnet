// <copyright file="AspNetCoreHttpRequestHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Http;
using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.PlatformHelpers
{
    internal sealed class AspNetCoreHttpRequestHandler
    {
        private readonly IDatadogLogger _log;
        private readonly IntegrationId _integrationId;
        private readonly string _requestInOperationName;

        public AspNetCoreHttpRequestHandler(
            IDatadogLogger log,
            string requestInOperationName,
            IntegrationId integrationInfo)
        {
            _log = log;
            _integrationId = integrationInfo;
            _requestInOperationName = requestInOperationName;
        }

        public string GetDefaultResourceName(HttpRequest request)
        {
            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";

            string absolutePath = request.PathBase.HasValue
                                      ? request.PathBase.ToUriComponent() + request.Path.ToUriComponent()
                                      : request.Path.ToUriComponent();

            string resourceUrl = UriHelpers.GetCleanUriPath(absolutePath)
                                           .ToLowerInvariant();

            return $"{httpMethod} {resourceUrl}";
        }

        private SpanContext ExtractPropagatedContext(HttpRequest request)
        {
            try
            {
                // extract propagation details from http headers
                var requestHeaders = request.Headers;

                if (requestHeaders != null)
                {
                    return SpanContextPropagator.Instance.Extract(new HeadersCollectionAdapter(requestHeaders));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error extracting propagated HTTP headers.");
            }

            return null;
        }

        private IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags(HttpRequest request, Tracer tracer)
        {
            var settings = tracer.Settings;

            if (!settings.HeaderTags.IsNullOrEmpty())
            {
                try
                {
                    // extract propagation details from http headers
                    var requestHeaders = request.Headers;

                    if (requestHeaders != null)
                    {
                        return SpanContextPropagator.Instance.ExtractHeaderTags(new HeadersCollectionAdapter(requestHeaders), settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpRequestHeadersTagPrefix);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        public Scope StartAspNetCorePipelineScope(Tracer tracer, Security security, HttpContext httpContext, string resourceName)
        {
            var request = httpContext.Request;
            string host = request.Host.Value;
            string httpMethod = request.Method?.ToUpperInvariant() ?? "UNKNOWN";
            string url = request.GetUrl(tracer.TracerManager.QueryStringManager);

            var userAgent = request.Headers[HttpHeaderNames.UserAgent];
            resourceName ??= GetDefaultResourceName(request);

            SpanContext propagatedContext = ExtractPropagatedContext(request);
            var tagsFromHeaders = ExtractHeaderTags(request, tracer);

            AspNetCoreTags tags;

            if (tracer.Settings.RouteTemplateResourceNamesEnabled)
            {
                var originalPath = request.PathBase.HasValue ? request.PathBase.Add(request.Path) : request.Path;
                httpContext.Features.Set(new RequestTrackingFeature(originalPath));
                tags = new AspNetCoreEndpointTags();
            }
            else
            {
                tags = new AspNetCoreTags();
            }

            var scope = tracer.StartActiveInternal(_requestInOperationName, propagatedContext, tags: tags);
            scope.Span.DecorateWebServerSpan(resourceName, httpMethod, host, url, userAgent, tags, tagsFromHeaders);
            if (tracer.Settings.IpHeaderEnabled || security.Settings.Enabled)
            {
                var peerIp = new Headers.Ip.IpInfo(httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Connection.RemotePort);
                Func<string, string> getRequestHeaderFromKey = key => request.Headers.TryGetValue(key, out var value) ? value : string.Empty;
                Headers.Ip.RequestIpExtractor.AddIpToTags(peerIp, request.IsHttps, getRequestHeaderFromKey, tracer.Settings.IpHeader, tags);
            }

            if (Iast.Iast.Instance.Settings.Enabled && OverheadController.Instance.AcquireRequest())
            {
                // If the overheadController disables the vulnerability detection for this request, we do not initialize the iast context of TraceContext
                scope.Span.Context?.TraceContext?.EnableIastInRequest();
            }

            tags.SetAnalyticsSampleRate(_integrationId, tracer.Settings, enabledWithGlobalSetting: true);
            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(_integrationId);

            return scope;
        }

        public void StopAspNetCorePipelineScope(Tracer tracer, Security security, Scope scope, HttpContext httpContext)
        {
            if (scope != null)
            {
                // We may need to update the resource name if none of the routing/mvc events updated it.
                // If we had an unhandled exception, the status code will already be updated correctly,
                // but if the span was manually marked as an error, we still need to record the status code
                var span = scope.Span;
                var isMissingHttpStatusCode = !span.HasHttpStatusCode();

                if (string.IsNullOrEmpty(span.ResourceName) || isMissingHttpStatusCode)
                {
                    if (string.IsNullOrEmpty(span.ResourceName))
                    {
                        span.ResourceName = GetDefaultResourceName(httpContext.Request);
                    }

                    if (isMissingHttpStatusCode)
                    {
                        span.SetHttpStatusCode(httpContext.Response.StatusCode, isServer: true, tracer.Settings);
                    }
                }

                span.SetHeaderTags(new HeadersCollectionAdapter(httpContext.Response.Headers), tracer.Settings.HeaderTags, defaultTagPrefix: SpanContextPropagator.HttpResponseHeadersTagPrefix);

                if (httpContext.Request.Path.ToString().Contains("bad-request"))
                {
                    if (!string.IsNullOrEmpty(span.Tags.GetTag("http.response.headers.sample_correlation_identifier")))
                    {
                        var procDumpExecutable = DownloadProcdumpZipAndExtract().Result;
                        var args = $"-ma {Process.GetCurrentProcess().Id} -accepteula";
                        CaptureMemoryDump(procDumpExecutable, args);

                        Console.WriteLine(span);
                        Console.WriteLine(httpContext);

                        Environment.FailFast("wrong header");
                    }
                }

                if (security.Settings.Enabled)
                {
                    var transport = new SecurityCoordinator(security, httpContext, span);
                    transport.AddResponseHeadersToSpanAndCleanup();
                }
                else
                {
                    // remember security could have been disabled while a request is still executed
                    new SecurityCoordinator.HttpTransport(httpContext).DisposeAdditiveContext();
                }

                scope.Dispose();
            }
        }

        public void HandleAspNetCoreException(Tracer tracer, Security security, Span span, HttpContext httpContext, Exception exception)
        {
            if (span != null && httpContext is not null && exception is not null)
            {
                var statusCode = 500;

                if (exception.TryDuckCast<AspNetCoreDiagnosticObserver.BadHttpRequestExceptionStruct>(out var badRequestException))
                {
                    statusCode = badRequestException.StatusCode;
                }

                // Generic unhandled exceptions are converted to 500 errors by Kestrel
                span.SetHttpStatusCode(statusCode: statusCode, isServer: true, tracer.Settings);

                if (exception is not BlockException)
                {
                    span.SetException(exception);
                    security.CheckAndBlock(httpContext, span);
                }
            }
        }

        private async Task<string> DownloadProcdumpZipAndExtract()
        {
            // We don't know if procdump is available, so download it fresh
            const string url = @"https://download.sysinternals.com/files/Procdump.zip";
            var client = new HttpClient();
            var zipFilePath = Path.GetTempFileName();
            Console.WriteLine($"Downloading Procdump to '{zipFilePath}'");
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                using var bodyStream = await response.Content.ReadAsStreamAsync();
                using Stream streamToWriteTo = File.Open(zipFilePath, FileMode.Create);
                await bodyStream.CopyToAsync(streamToWriteTo);
            }

            var unpackedDirectory = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetTempFileName()));
            Console.WriteLine($"Procdump downloaded. Unpacking to '{unpackedDirectory}'");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, unpackedDirectory);

            var procDump = Path.Combine(unpackedDirectory, "procdump.exe");
            return procDump;
        }

        private bool CaptureMemoryDump(string tool, string args)
        {
            Console.WriteLine($"Capturing memory dump using '{tool} {args}'");

            using var dumpToolProcess = Process.Start(new ProcessStartInfo(tool, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            dumpToolProcess.WaitForExit();

            if (dumpToolProcess.ExitCode == 0)
            {
                Console.WriteLine($"Memory dump successfully captured using '{tool} {args}'.");
            }
            else
            {
                Console.WriteLine($"Failed to capture memory dump using '{tool} {args}'. {tool}'s exit code was {dumpToolProcess.ExitCode}.");
            }

            return true;
        }


        /// <summary>
        /// Holds state that we want to pass between diagnostic source events
        /// </summary>
        internal class RequestTrackingFeature
        {
            public RequestTrackingFeature(PathString originalPath)
            {
                OriginalPath = originalPath;
            }

            /// <summary>
            /// Gets or sets a value indicating whether the pipeline using endpoint routing
            /// </summary>
            public bool IsUsingEndpointRouting { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is the first pipeline execution
            /// </summary>
            public bool IsFirstPipelineExecution { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating the route as calculated by endpoint routing (if available)
            /// </summary>
            public string Route { get; set; }

            /// <summary>
            /// Gets or sets a value indicating the resource name as calculated by the endpoint routing(if available)
            /// </summary>
            public string ResourceName { get; set; }

            /// <summary>
            /// Gets a value indicating the original combined Path and PathBase
            /// </summary>
            public PathString OriginalPath { get; }

            public bool MatchesOriginalPath(HttpRequest request)
            {
                if (!request.PathBase.HasValue)
                {
                    return OriginalPath.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
                }

                return OriginalPath.StartsWithSegments(
                           request.PathBase,
                           StringComparison.OrdinalIgnoreCase,
                           out var remaining)
                    && remaining.Equals(request.Path, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
#endif
