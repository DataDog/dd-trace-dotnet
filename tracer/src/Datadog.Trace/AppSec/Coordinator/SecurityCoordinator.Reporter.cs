// <copyright file="SecurityCoordinator.Reporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using Datadog.Trace.AppSec.AttackerFingerprint;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Coordinator;

internal readonly partial struct SecurityCoordinator
{
    private const int MaxApiSecurityTagValueLength = 25000;

    private static readonly Dictionary<string, string?> RequestHeaders = new()
    {
        { "x-forwarded-for", string.Empty },
        { "x-real-ip", string.Empty },
        { "true-client-ip", string.Empty },
        { "x-client-ip", string.Empty },
        { "x-forwarded", string.Empty },
        { "forwarded-for", string.Empty },
        { "x-cluster-client-ip", string.Empty },
        { "fastly-client-ip", string.Empty },
        { "cf-connecting-ip", string.Empty },
        { "cf-connecting-ipv6", string.Empty },
        { "forwarded", string.Empty },
        { "via", string.Empty },
        { "Content-Length", string.Empty },
        { "Content-Type", string.Empty },
        { "Content-Encoding", string.Empty },
        { "Content-Language", string.Empty },
        { "Host", string.Empty },
        { "user-agent", string.Empty },
        { "Accept", string.Empty },
        { "Accept-Encoding", string.Empty },
        { "Accept-Language", string.Empty },
    };

    private static readonly Dictionary<string, string?> ExternalWafsRequestHeaders = new()
    {
        { "Akamai-User-Risk", string.Empty },
        { "CF-ray", string.Empty },
        { "Cloudfront-Viewer-Ja3-Fingerprint", string.Empty },
        { "X-Amzn-Trace-Id", string.Empty },
        { "X-Appgw-Trace-id", string.Empty },
        { "X-Cloud-Trace-Context", string.Empty },
        { "X-SigSci-RequestID", string.Empty },
        { "X-SigSci-Tags", string.Empty },
    };

    private static readonly Dictionary<string, string?> ResponseHeaders = new() { { "content-length", string.Empty }, { "content-type", string.Empty }, { "Content-Encoding", string.Empty }, { "Content-Language", string.Empty } };

    private static void AddHeaderTags(Span span, IHeadersCollection headers, Dictionary<string, string?> headersToCollect, string prefix)
    {
        SpanContextPropagator.Instance.AddHeadersToSpanAsTags(span, headers, headersToCollect, defaultTagPrefix: prefix);
    }

    private static void LogAddressIfDebugEnabled(IDictionary<string, object> args)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            foreach (var key in args.Keys)
            {
                Log.Debug("DDAS-0008-00: Pushing address {Key} to the Instrumentation Gateway.", key);
            }
        }
    }

    internal static void ReportWafInitInfoOnce(Security security, Span span)
    {
        if (security.WafInitResult is { Reported: false })
        {
            span = TryGetRoot(span);
            security.WafInitResult.Reported = true;
            span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            span.SetMetric(Metrics.AppSecWafInitRulesLoaded, security.WafInitResult.LoadedRules);
            span.SetMetric(Metrics.AppSecWafInitRulesErrorCount, security.WafInitResult.FailedToLoadRules);
            if (security.WafInitResult.HasErrors && !Security.HasOnlyUnknownMatcherErrors(security.WafInitResult.Errors))
            {
                span.SetTag(Tags.AppSecWafInitRuleErrors, security.WafInitResult.ErrorMessage);
            }

            span.SetTag(Tags.AppSecWafVersion, security.DdlibWafVersion);
        }
    }

    /// <summary>
    /// This functions reports the security scan result and the schema extraction if there was one
    /// Dont try to test if result should be reported, it's all in here
    /// </summary>
    /// <param name="result">waf's result</param>
    /// <param name="blocked">if request was blocked</param>
    /// <param name="status">returned status code</param>
    internal void TryReport(IResult result, bool blocked, int? status = null)
    {
        IHeadersCollection? headers = null;
        if (!_httpTransport.ReportedExternalWafsRequestHeaders)
        {
            headers = _httpTransport.GetRequestHeaders();
            AddHeaderTags(_localRootSpan, headers, ExternalWafsRequestHeaders, SpanContextPropagator.HttpRequestHeadersTagPrefix);
            _httpTransport.ReportedExternalWafsRequestHeaders = true;
        }

        AttackerFingerprintHelper.AddSpanTags(_localRootSpan, result);

        if (result.ShouldReportSecurityResult)
        {
            _localRootSpan.SetTag(Tags.AppSecEvent, "true");
            if (blocked)
            {
                _localRootSpan.SetTag(Tags.AppSecBlocked, "true");
            }

            _security.SetTraceSamplingPriority(_localRootSpan);

            LogMatchesIfDebugEnabled(result.Data, blocked);

            var traceContext = _localRootSpan.Context.TraceContext;
            if (result.Data != null)
            {
                traceContext.AppSecRequestContext.AddWafSecurityEvents(result.Data);
            }

            var clientIp = _localRootSpan.GetTag(Tags.HttpClientIp);
            if (!string.IsNullOrEmpty(clientIp))
            {
                _localRootSpan.SetTag(Tags.ActorIp, clientIp);
            }

            if (traceContext is { Origin: null })
            {
                _localRootSpan.SetTag(Tags.Origin, "appsec");
                traceContext.Origin = "appsec";
            }

            _localRootSpan.SetTag(Tags.AppSecRuleFileVersion, _security.WafRuleFileVersion);
            _localRootSpan.SetMetric(Metrics.AppSecWafDuration, result.AggregatedTotalRuntime);
            _localRootSpan.SetMetric(Metrics.AppSecWafAndBindingsDuration, result.AggregatedTotalRuntimeWithBindings);
            headers ??= _httpTransport.GetRequestHeaders();
            AddHeaderTags(_localRootSpan, headers, RequestHeaders, SpanContextPropagator.HttpRequestHeadersTagPrefix);

            if (status is not null)
            {
                _localRootSpan.SetHttpStatusCode(status.Value, isServer: true, Tracer.Instance.Settings);
            }
        }

        AddRaspSpanMetrics(result, _localRootSpan);

        if (result.ExtractSchemaDerivatives?.Count > 0)
        {
            foreach (var derivative in result.ExtractSchemaDerivatives)
            {
                var serializeObject = JsonConvert.SerializeObject(derivative.Value);
                var bytes = System.Text.Encoding.UTF8.GetBytes(serializeObject);
                if (bytes.Length <= MaxApiSecurityTagValueLength)
                {
                    var memoryStream = new MemoryStream();
                    using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                    {
                        gZipStream.Write(bytes, 0, bytes.Length);
                    }

                    if (memoryStream.TryGetBuffer(out var bytesResult))
                    {
                        var gzipBase64 = Convert.ToBase64String(bytesResult.Array!, bytesResult.Offset, bytesResult.Count);
                        _localRootSpan.SetTag(derivative.Key, gzipBase64);
                    }
                    else
                    {
                        Log.Debug("Could not TryGetBuffer from the appsec schema extraction memoryStream");
                    }
                }
            }
        }
    }

    private void AddRaspSpanMetrics(IResult result, Span localRootSpan)
    {
        // We don't want to fill the spans with not useful data, so we only send it when RASP has been used
        // We report always, even if there is no match
        if (result.AggregatedTotalRuntimeRasp > 0)
        {
            localRootSpan.Context.TraceContext.AppSecRequestContext.AddRaspSpanMetrics(result.AggregatedTotalRuntimeRasp, result.AggregatedTotalRuntimeWithBindingsRasp, result.Timeout);
        }
    }

    private void AddResponseHeaderTags(bool canAccessHeaders)
    {
        TryAddEndPoint();
        var headers = canAccessHeaders ? _httpTransport.GetResponseHeaders() : new NameValueHeadersCollection(new NameValueCollection());
        AddHeaderTags(_localRootSpan, headers, ResponseHeaders, SpanContextPropagator.HttpResponseHeadersTagPrefix);
    }

    private void TryAddEndPoint()
    {
        var route = _localRootSpan.GetTag(Tags.AspNetCoreRoute) ?? _localRootSpan.GetTag(Tags.AspNetRoute);
        if (route != null)
        {
            _localRootSpan.SetTag(Tags.HttpEndpoint, route);
        }
    }
}
