// <copyright file="SecurityCoordinator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#pragma warning disable CS0282
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
#else
using System.Web;
#endif

namespace Datadog.Trace.AppSec.Coordinator;

/// <summary>
/// Bridge class between security components and http transport classes, that calls security and is responsible for reporting
/// </summary>
internal readonly partial struct SecurityCoordinator
{
    private const string ReportedExternalWafsRequestHeadersStr = "ReportedExternalWafsRequestHeaders";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SecurityCoordinator>();
    private readonly Security _security;
    private readonly Span _localRootSpan;
    private readonly HttpTransportBase _httpTransport;

    public bool IsBlocked => _httpTransport.IsBlocked;

    public SecurityReporter Reporter { get; }

    public void MarkBlocked() => _httpTransport.MarkBlocked();

    public IResult? Scan(bool lastTime = false)
    {
        var args = GetBasicRequestArgsForWaf();
        return RunWaf(args, lastTime);
    }

    public bool IsAdditiveContextDisposed() => _httpTransport.IsAdditiveContextDisposed();

    public IResult? RunWaf(Dictionary<string, object> args, bool lastWafCall = false, bool runWithEphemeral = false, bool isRasp = false)
    {
        if (_httpTransport.ContextUninitialized)
        {
            Log.Debug("Trying to call the WAF with an unitialized context.");
            return null;
        }

        SecurityReporter.LogAddressIfDebugEnabled(args);
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
            else if (_httpTransport.IsAdditiveContextDisposed())
            {
                Log.Warning("Waf could not run as waf additive context is disposed");
                return null;
            }

            _security.ApiSecurity.ShouldAnalyzeSchema(lastWafCall, _localRootSpan, args, _httpTransport.StatusCode.ToString(), _httpTransport.RouteData);

            if (additiveContext != null)
            {
                // run the WAF and execute the results
                if (runWithEphemeral)
                {
                    result = additiveContext.RunWithEphemeral(args, _security.Settings.WafTimeoutMicroSeconds, isRasp);
                }
                else
                {
                    result = additiveContext.Run(args, _security.Settings.WafTimeoutMicroSeconds);
                }

                SecurityReporter.RecordTelemetry(result);
            }
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            var stringBuilder = StringBuilderCache.Acquire();
            foreach (var kvp in args)
            {
                stringBuilder.Append($"Key: {kvp.Key} Value: {kvp.Value}, ");
            }

            Log.Error(ex, "Call into the security module failed with arguments {Args}", StringBuilderCache.GetStringAndRelease(stringBuilder));
        }

        if (_localRootSpan.Context.TraceContext is not null)
        {
            _localRootSpan.Context.TraceContext.WafExecuted = true;
        }

        return result;
    }

    internal static Span TryGetRoot(Span span) => span.Context.TraceContext?.RootSpan ?? span;

    internal static Dictionary<string, object>? ExtractCookiesFromRequest(HttpRequest request)
    {
        var cookies = RequestDataHelper.GetCookies(request);

        if (cookies is { Count: > 0 })
        {
            var cookiesCount = cookies.Count;
            var cookiesDic = new Dictionary<string, object>(cookiesCount);
            for (var i = 0; i < cookiesCount; i++)
            {
                GetCookieKeyValueFromIndex(cookies, i, out var keyForDictionary, out var cookieValue);

                if (cookieValue is not null && keyForDictionary is not null)
                {
                    if (!cookiesDic.TryGetValue(keyForDictionary, out var value))
                    {
                        cookiesDic.Add(keyForDictionary, cookieValue);
                    }
                    else
                    {
                        if (value is string stringValue)
                        {
                            cookiesDic[keyForDictionary] = new List<string> { stringValue, cookieValue };
                        }
                        else if (value is List<string> valueList)
                        {
                            valueList.Add(cookieValue);
                        }
                        else
                        {
                            Log.Warning("Cookie {Key} couldn't be added as argument to the waf", keyForDictionary);
                        }
                    }
                }
            }

            return cookiesDic;
        }

        return null;
    }

    private static Dictionary<string, object> ExtractHeaders(ICollection<string> keys, Func<string, object> getHeaderValue)
    {
        var headersDic = new Dictionary<string, object>(keys.Count);
        foreach (var key in keys)
        {
            var currentKey = key ?? string.Empty;
            if (!currentKey.Equals("cookie", StringComparison.OrdinalIgnoreCase))
            {
                currentKey = currentKey.ToLowerInvariant();
                var value = getHeaderValue(currentKey);

                if (value is not null)
                {
                    if (!headersDic.ContainsKey(currentKey))
                    {
                        headersDic.Add(currentKey, value);
                    }
                    else
                    {
                        Log.Warning("Header {Key} couldn't be added as argument to the waf", currentKey);
                    }
                }
            }
        }

        return headersDic;
    }
}
