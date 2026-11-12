// <copyright file="AppSecRequestContext.WafRequests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.AppSec;

/// <summary>
/// Aggregates the outcome of the request for appsec.waf.requests, which counts requests rather than WAF runs:
/// its tags sum up all the non-RASP WAF runs of the request, plus what happened after them (the AppSec rate
/// limiter and the blocking), so it can only be reported when the request ends.
/// </summary>
internal sealed partial class AppSecRequestContext
{
    // the bits of the flags part of MetricTags.WafAnalysis
    private const int WafTimeoutFlag = 1;
    private const int RateLimitedFlag = 2;
    private const int InputTruncatedFlag = 4;
    private const int WafErrorFlag = 8;

    private bool _wafRan;
    private bool _ruleTriggered;
    private bool _blockRequested;
    private bool _blockFailed;
    private bool _wafTimedOut;
    private bool _wafFailed;
    private bool _inputTruncated;
    private bool _traceRateLimited;
    private bool _traceKept;
    private bool _wafRequestsReported;

    /// <summary>
    /// Records a non-RASP WAF run of the request
    /// </summary>
    internal void RecordWafRun(IResult result)
    {
        lock (_sync)
        {
            _wafRan = true;
            _ruleTriggered |= result.ShouldReportSecurityResult || result.ShouldBlock;
            _blockRequested |= result.ShouldBlock;
            _inputTruncated |= result.Truncated;
            _wafTimedOut |= result.Timeout;
            _wafFailed |= SecurityReporter.IsWafError(result);
        }
    }

    /// <summary>
    /// Records that the AppSec rate limiter kept the trace
    /// </summary>
    internal void MarkTraceKept()
    {
        lock (_sync)
        {
            _traceKept = true;
        }
    }

    /// <summary>
    /// Records that the AppSec rate limiter refused to keep the trace
    /// </summary>
    internal void MarkTraceRateLimited()
    {
        lock (_sync)
        {
            _traceRateLimited = true;
        }
    }

    /// <summary>
    /// Records that a block, from the WAF or from RASP, could not be applied to the response
    /// </summary>
    internal void MarkBlockFailed()
    {
        lock (_sync)
        {
            _blockFailed = true;
        }
    }

    // must be called with _sync held, once per request
    private void ReportWafRequests(Span span, IMetricsTelemetryCollector metrics)
    {
        if (_blockFailed)
        {
            span.Tags.SetMetric(Metrics.AppSecBlockFailed, 1);
        }

        if (!_wafRan || _wafRequestsReported)
        {
            return;
        }

        _wafRequestsReported = true;
        metrics.RecordCountWafRequests(GetWafAnalysisTag());
    }

    private MetricTags.WafAnalysis GetWafAnalysisTag()
    {
        // the outcomes are the blocks of 16 of MetricTags.WafAnalysis: Normal, RuleTriggered, Blocked and
        // BlockFailure. A failed block only counts when the WAF asked for it, the tags don't cover RASP.
        var outcome = !_ruleTriggered ? 0
                      : !_blockRequested ? 1
                      : _blockFailed ? 3
                      : 2;

        var flags = (_wafTimedOut ? WafTimeoutFlag : 0)
                  | (_traceRateLimited && !_traceKept ? RateLimitedFlag : 0)
                  | (_inputTruncated ? InputTruncatedFlag : 0)
                  | (_wafFailed ? WafErrorFlag : 0);

        return (MetricTags.WafAnalysis)((outcome * 16) + flags);
    }
}
