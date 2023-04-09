// <copyright file="SecurityCoordinator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#pragma warning disable CS0282
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Coordinator;

/// <summary>
/// Bridge class between security components and http transport classes, that calls security and is responsible for reporting
/// </summary>
internal readonly partial struct SecurityCoordinator
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SecurityCoordinator>();
    private readonly Security _security;
    private readonly Span _localRootSpan;
    private readonly HttpTransportBase _httpTransport;

    public bool IsBlocked => _httpTransport.IsBlocked;

    public void MarkBlocked() => _httpTransport.MarkBlocked();

    private static void LogMatchesIfDebugEnabled(string? result, bool blocked)
    {
        if (Log.IsEnabled(LogEventLevel.Debug) && result != null)
        {
            var results = JsonConvert.DeserializeObject<WafMatch[]>(result);
            for (var i = 0; i < results?.Length; i++)
            {
                var match = results[i];
                if (blocked)
                {
                    Log.Debug("DDAS-0012-02: Blocking current transaction (rule: {RuleId})", match.Rule);
                }
                else
                {
                    Log.Debug("DDAS-0012-01: Detecting an attack from rule {RuleId}", match.Rule);
                }
            }
        }
    }

    public IResult? Scan()
    {
        var args = GetBasicRequestArgsForWaf();
        return RunWaf(args);
    }

    public IResult? RunWaf(Dictionary<string, object> args)
    {
        LogAddressIfDebugEnabled(args);
        IResult? result = null;
        try
        {
            var additiveContext = _httpTransport.GetAdditiveContext();

            if (additiveContext == null)
            {
                additiveContext = _security.CreateAdditiveContext();
                // prevent very cases where waf has been disposed between here and has been passed as argument until the 2nd line of constructor..
                if (additiveContext != null)
                {
                    _httpTransport.SetAdditiveContext(additiveContext);
                }
            }

            if (additiveContext != null)
            {
                // run the WAF and execute the results
                result = additiveContext.Run(args, _security.Settings.WafTimeoutMicroSeconds);
            }
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            Log.Error(ex, "Call into the security module failed");
        }
        finally
        {
            // annotate span
            _localRootSpan.SetMetric(Metrics.AppSecEnabled, 1.0);
            _localRootSpan.SetTag(Tags.RuntimeFamily, TracerConstants.Language);
        }

        return result;
    }

    public void AddResponseHeadersToSpanAndCleanup()
    {
        if (_localRootSpan.IsAppsecEvent())
        {
            AddResponseHeaderTags(CanAccessHeaders);
        }

        _httpTransport.DisposeAdditiveContext();
    }

    private static Span TryGetRoot(Span span) => span.Context.TraceContext?.RootSpan ?? span;
}
