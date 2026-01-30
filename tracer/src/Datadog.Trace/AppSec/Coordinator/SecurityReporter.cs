// <copyright file="SecurityReporter.cs" company="Datadog">
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
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Coordinator;

internal sealed partial class SecurityReporter
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
    private readonly HttpTransportBase _httpTransport;
    private readonly Span _span;

    internal SecurityReporter(Span span, HttpTransportBase httpTransport, bool isRoot = false)
    {
        _span = isRoot ? span : SecurityCoordinator.TryGetRoot(span);
        _httpTransport = httpTransport;
    }

    internal static void LogAddressIfDebugEnabled(IDictionary<string, object> args)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            foreach (var key in args.Keys)
            {
                Log.Debug("DDAS-0008-00: Pushing address {Key} to the Instrumentation Gateway.", key);
            }
        }
    }

    internal void AddRequestHeaders(IHeadersCollection headers) => AddHeaderTags(_span, headers, RequestHeaders, SpanContextPropagator.HttpRequestHeadersTagPrefix);

    internal void AddResponseHeadersToSpan()
    {
        if (_span.IsAppsecEvent())
        {
            AddResponseHeaderTags();
        }
    }

    private static void AddHeaderTags(Span span, IHeadersCollection headers, Dictionary<string, string?> headersToCollect, string prefix) => Tracer.Instance.TracerManager.SpanContextPropagator.AddHeadersToSpanAsTags(span, headers, headersToCollect, defaultTagPrefix: prefix);

    private static void LogMatchesIfDebugEnabled(IReadOnlyCollection<object>? results, bool blocked)
    {
        if (Log.IsEnabled(LogEventLevel.Debug) && results != null)
        {
            foreach (var result in results)
            {
                if (result is Dictionary<string, object?> match)
                {
                    if (blocked)
                    {
                        Log.Debug("DDAS-0012-02: Blocking current transaction (rule: {RuleId})", match["rule"]);
                    }
                    else
                    {
                        Log.Debug("DDAS-0012-01: Detecting an attack from rule {RuleId}", match["rule"]);
                    }
                }
                else
                {
                    Log.Debug("{Result} not of expected type", result);
                }
            }
        }
    }

    internal static void RecordWafTelemetry(IResult? result)
    {
        if (result is null)
        {
            return;
        }

        if (result.Timeout)
        {
            TelemetryFactory.Metrics.RecordCountWafRequests(
                result.Truncated ? MetricTags.WafAnalysis.WafTimeoutTruncated : MetricTags.WafAnalysis.WafTimeout);
        }
        else if (result.ShouldBlock)
        {
            TelemetryFactory.Metrics.RecordCountWafRequests(
                result.Truncated ? MetricTags.WafAnalysis.RuleTriggeredAndBlockedTruncated : MetricTags.WafAnalysis.RuleTriggeredAndBlocked);
        }
        else if (result.ShouldReportSecurityResult)
        {
            TelemetryFactory.Metrics.RecordCountWafRequests(
                result.Truncated ? MetricTags.WafAnalysis.RuleTriggeredTruncated : MetricTags.WafAnalysis.RuleTriggered);
        }
        else
        {
            TelemetryFactory.Metrics.RecordCountWafRequests(
                result.Truncated ? MetricTags.WafAnalysis.NormalTruncated : MetricTags.WafAnalysis.Normal);
        }
    }

    internal void ReportWafInitInfoOnce(InitResult? initResult)
    {
        if (initResult is { Reported: false })
        {
            _span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            _span.SetMetric(Metrics.AppSecWafInitRulesLoaded, initResult.LoadedRules);
            _span.SetMetric(Metrics.AppSecWafInitRulesErrorCount, initResult.FailedToLoadRules);
            if (initResult.HasErrors && !Security.HasOnlyUnknownMatcherErrors(initResult.Errors))
            {
                _span.SetTag(Tags.AppSecWafInitRuleErrors, initResult.ErrorMessage);
            }

            _span.SetTag(Tags.AppSecWafVersion, initResult.Waf?.Version);
        }
    }

    /// <summary>
    /// This functions reports the security scan result and the schema extraction if there was one
    /// Don't try to test if result should be reported, it's all in here
    /// </summary>
    /// <param name="result">waf's result</param>
    /// <param name="blocked">if request was blocked</param>
    /// <param name="status">returned status code</param>
    internal void TryReport(IResult result, bool blocked, int? status = null)
    {
        IHeadersCollection? headers = null;
        if (_httpTransport is { ReportedExternalWafsRequestHeaders: false, IsHttpContextDisposed: false })
        {
            headers = _httpTransport.GetRequestHeaders();
            if (headers is not null)
            {
                AddHeaderTags(_span, headers, ExternalWafsRequestHeaders, SpanContextPropagator.HttpRequestHeadersTagPrefix);
                _httpTransport.ReportedExternalWafsRequestHeaders = true;
            }
        }

        AttackerFingerprintHelper.AddSpanTags(_span, result);

        if (result.ShouldReportSecurityResult)
        {
            _span.SetTag(Tags.AppSecEvent, "true");
            if (blocked)
            {
                _span.SetTag(Tags.AppSecBlocked, "true");
            }

            Security.Instance?.SetTraceSamplingPriority(_span);

            LogMatchesIfDebugEnabled(result.Data, blocked);

            var traceContext = _span.Context.TraceContext;
            if (result.Data != null)
            {
                traceContext.AppSecRequestContext.AddWafSecurityEvents(result.Data);
            }

            var clientIp = _span.GetTag(Tags.HttpClientIp);
            if (!string.IsNullOrEmpty(clientIp))
            {
                _span.SetTag(Tags.ActorIp, clientIp);
            }

            if (traceContext is { Origin: null })
            {
                _span.SetTag(Tags.Origin, "appsec");
                traceContext.Origin = "appsec";
            }

            _span.SetMetric(Metrics.AppSecWafDuration, result.AggregatedTotalRuntime);
            _span.SetMetric(Metrics.AppSecWafAndBindingsDuration, result.AggregatedTotalRuntimeWithBindings);

            if (headers is null && !_httpTransport.IsHttpContextDisposed)
            {
                headers = _httpTransport.GetRequestHeaders();
            }

            if (headers is not null)
            {
                AddHeaderTags(_span, headers, RequestHeaders, SpanContextPropagator.HttpRequestHeadersTagPrefix);
            }

            if (status is not null)
            {
                _span.SetHttpStatusCode(status.Value, isServer: true, Tracer.Instance.CurrentTraceSettings.Settings);
            }
        }

        AddRaspSpanMetrics(result, _span);

        if (result.ExtractSchemaDerivatives?.Count > 0)
        {
            bool written = false;
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
                        _span.SetTag(derivative.Key, gzipBase64);
                        written = true;
                    }
                    else
                    {
                        Log.Debug("Could not TryGetBuffer from the appsec schema extraction memoryStream");
                    }
                }
            }

            if (written)
            {
                Security.Instance?.SetTraceSamplingPriority(_span, false); // Avoid downstream propagation in Standalone mode
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

    internal void AddResponseHeaderTags()
    {
        TryAddEndPoint();
        var headers = CanAccessHeaders ? _httpTransport.GetResponseHeaders() : new NameValueHeadersCollection(new NameValueCollection());
        AddHeaderTags(_span, headers, ResponseHeaders, SpanContextPropagator.HttpResponseHeadersTagPrefix);
    }

    private void TryAddEndPoint()
    {
        var route = _span.GetTag(Tags.AspNetCoreRoute) ?? _span.GetTag(Tags.AspNetRoute);
        if (route != null)
        {
            _span.SetTag(Tags.HttpEndpoint, route);
        }
    }
}
