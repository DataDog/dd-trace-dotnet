// <copyright file="SecurityCoordinator.Reporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Coordinator;

internal readonly partial struct SecurityCoordinator
{
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

    private static readonly Dictionary<string, string?> ResponseHeaders = new() { { "content-length", string.Empty }, { "content-type", string.Empty }, { "Content-Encoding", string.Empty }, { "Content-Language", string.Empty } };

    private static void AddHeaderTags(Span span, IHeadersCollection headers, Dictionary<string, string?> headersToCollect, string prefix)
    {
        var tags = SpanContextPropagator.Instance.ExtractHeaderTags(headers, headersToCollect, defaultTagPrefix: prefix);
        foreach (var tag in tags)
        {
            span.SetTag(tag.Key, tag.Value);
        }
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

    public void Report(string triggerData, ulong aggregatedTotalRuntime, ulong aggregatedTotalRuntimeWithBindings, bool blocked)
    {
        _localRootSpan.SetTag(Tags.AppSecEvent, "true");
        if (blocked)
        {
            _localRootSpan.SetTag(Tags.AppSecBlocked, "true");
        }

        _security.SetTraceSamplingPriority(_localRootSpan);

        LogMatchesIfDebugEnabled(triggerData, blocked);

        _localRootSpan.SetTag(Tags.AppSecJson, "{\"triggers\":" + triggerData + "}");
        var clientIp = _localRootSpan.GetTag(Tags.HttpClientIp);
        if (!string.IsNullOrEmpty(clientIp))
        {
            _localRootSpan.SetTag(Tags.ActorIp, clientIp);
        }

        if (_localRootSpan.Context.TraceContext is { Origin: null } traceContext)
        {
            _localRootSpan.SetTag(Tags.Origin, "appsec");
            traceContext.Origin = "appsec";
        }

        _localRootSpan.SetTag(Tags.AppSecRuleFileVersion, _security.WafRuleFileVersion);
        _localRootSpan.SetMetric(Metrics.AppSecWafDuration, aggregatedTotalRuntime);
        _localRootSpan.SetMetric(Metrics.AppSecWafAndBindingsDuration, aggregatedTotalRuntimeWithBindings);
        var headers = _httpTransport.GetRequestHeaders();
        AddHeaderTags(_localRootSpan, headers, RequestHeaders, SpanContextPropagator.HttpRequestHeadersTagPrefix);
    }

    public void AddResponseHeaderTags(bool canAccessHeaders)
    {
        TryAddEndPoint();
        var headers = canAccessHeaders ? _httpTransport.GetResponseHeaders() : new NameValueHeadersCollection(new NameValueCollection());
        AddHeaderTags(_localRootSpan, headers, ResponseHeaders, SpanContextPropagator.HttpResponseHeadersTagPrefix);
    }

    internal static void ReportWafInitInfoOnce(Security security, Span span)
    {
        if (!security.WafInitResult.Reported)
        {
            span = TryGetRoot(span);
            security.WafInitResult.Reported = true;
            span.Context.TraceContext?.SetSamplingPriority(SamplingPriorityValues.UserKeep, SamplingMechanism.Asm);
            span.SetMetric(Metrics.AppSecWafInitRulesLoaded, security.WafInitResult.LoadedRules);
            span.SetMetric(Metrics.AppSecWafInitRulesErrorCount, security.WafInitResult.FailedToLoadRules);
            if (security.WafInitResult.HasErrors)
            {
                span.SetTag(Tags.AppSecWafInitRuleErrors, security.WafInitResult.ErrorMessage);
            }

            span.SetTag(Tags.AppSecWafVersion, security.DdlibWafVersion);
        }
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
