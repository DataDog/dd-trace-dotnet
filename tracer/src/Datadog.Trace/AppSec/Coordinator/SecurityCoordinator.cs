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
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
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
    private static bool _nullContextReported;
    private readonly Security _security;
    private readonly Span _localRootSpan;
    private readonly HttpTransportBase _httpTransport;
    private readonly AppSecRequestContext _appsecRequestContext;

    public bool IsBlocked => _httpTransport.IsBlocked;

    public SecurityReporter Reporter { get; }

    public void MarkBlocked() => _httpTransport.MarkBlocked();

    public IResult? Scan(bool lastTime = false)
    {
        var args = CollectRequestArgsForWaf();

        if (lastTime && _httpTransport.StatusCode is { } statusCode)
        {
            args[AddressesConstants.ResponseStatus] = statusCode.ToString();
        }

        return args.Count > 0 ? RunWaf(args, lastTime) : null;
    }

    /// <summary>
    /// Returns the request address set the first time it is called for a request, and an empty set
    /// afterwards, so that a second run doesn't pay for re-evaluating every request rule. The context
    /// check comes first so the addresses aren't marked as sent when there is no store to keep them.
    /// </summary>
    internal Dictionary<string, object> CollectRequestArgsForWaf() =>
        _appsecRequestContext.GetOrCreateAdditiveContext(_security) is not null && _appsecRequestContext.ShouldSendRequestAddresses()
            ? GetBasicRequestArgsForWaf()
            : new Dictionary<string, object>(3);

    internal bool ShouldScanResponse() => _appsecRequestContext.ShouldScanResponse();

    public IResult? RunWaf(Dictionary<string, object> args, bool lastWafCall = false, bool runWithEphemeral = false, bool isRasp = false, string? sessionId = null)
    {
        SecurityReporter.LogAddressIfDebugEnabled(args);
        IResult? result = null;

        try
        {
            var additiveContext = _appsecRequestContext.GetOrCreateAdditiveContext(_security);

            if (additiveContext is null)
            {
                return null;
            }

            var shouldAddSessionId = additiveContext.ShouldRunWithSession(_security, sessionId);
            if (shouldAddSessionId)
            {
                args[AddressesConstants.UserSessionId] = sessionId!;
            }

            _security.ApiSecurity.ShouldAnalyzeSchema(lastWafCall, _localRootSpan, args, _httpTransport.StatusCode, _httpTransport.RouteData);

            // run the WAF and execute the results
            result = runWithEphemeral
                         ? additiveContext.RunWithEphemeral(args, _security.Settings.WafTimeoutMicroSeconds, isRasp)
                         : additiveContext.Run(args, _security.Settings.WafTimeoutMicroSeconds);

            SetErrorInformation(isRasp, result);
            SecurityReporter.RecordWafTelemetry(result, isRasp);
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            if (result is null && !isRasp)
            {
                TelemetryFactory.Metrics.RecordCountWafError(MetricTags.WafError.BindingError);
            }

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

    private void SetErrorInformation(bool isRasp, IResult? result)
    {
        if (result is not null)
        {
            _localRootSpan.Context.TraceContext.AppSecRequestContext.CheckWAFError(result, isRasp);
        }
    }

    internal static Span TryGetRoot(Span span) => span.Context.TraceContext?.RootSpan ?? span;

    public IResult? RunWafForUser(string? userId = null, string? userLogin = null, string? userSessionId = null, bool fromSdk = false, Dictionary<string, string>? otherTags = null)
    {
        if (_httpTransport.IsBlocked)
        {
            return null;
        }

        IResult? result = null;
        Dictionary<string, object>? addresses = null;
        try
        {
            var additiveContext = _appsecRequestContext.GetOrCreateAdditiveContext(_security);
            if (additiveContext?.FilterAddresses(_security, userId, userLogin, userSessionId, fromSdk) is { Count: > 0 } userAddresses)
            {
                if (otherTags is not null)
                {
                    foreach (var kvp in otherTags)
                    {
#if NETCOREAPP
                        userAddresses.TryAdd(kvp.Key, kvp.Value);
#else
                        if (!userAddresses.ContainsKey(kvp.Key))
                        {
                            userAddresses.Add(kvp.Key, kvp.Value);
                        }
#endif
                    }
                }

                SecurityReporter.LogAddressIfDebugEnabled(userAddresses);

                // run the WAF and execute the results
                result = additiveContext.Run(userAddresses, _security.Settings.WafTimeoutMicroSeconds);
                SetErrorInformation(false, result);
                additiveContext.CommitUserRuns(userAddresses, fromSdk);
                SecurityReporter.RecordWafTelemetry(result, isRasp: false);

                if (_localRootSpan.Context.TraceContext is not null)
                {
                    _localRootSpan.Context.TraceContext.WafExecuted = true;
                }
            }
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            if (result is null)
            {
                TelemetryFactory.Metrics.RecordCountWafError(MetricTags.WafError.BindingError);
            }

            if (addresses is not null)
            {
                var stringBuilder = StringBuilderCache.Acquire();
                foreach (var kvp in addresses)
                {
                    stringBuilder.Append($"Key: {kvp.Key} Value: {kvp.Value}, ");
                }

                Log.Error(ex, "Call into the security module failed with arguments {Args}", StringBuilderCache.GetStringAndRelease(stringBuilder));
            }
        }

        return result;
    }

    public void AddResponseHeadersToSpan() => Reporter.AddResponseHeadersToSpan();

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

#pragma warning disable CA1859 // Use concrete types where possible for improved performance - It's not actually possible here
    private static Dictionary<string, object>? ExtractHeaders(ICollection<string> keys, object collection, Func<object, string, object> getHeaderValue)
#pragma warning restore CA1859
    {
        if (keys.Count > 0)
        {
            var headersDic = new Dictionary<string, object>(keys.Count);
            foreach (var key in keys)
            {
                var currentKey = key ?? string.Empty;
                if (!currentKey.Equals("cookie", StringComparison.OrdinalIgnoreCase))
                {
                    currentKey = currentKey.ToLowerInvariant();
                    var value = getHeaderValue(collection, currentKey);
#if NETCOREAPP
                    if (!headersDic.TryAdd(currentKey, value))
                    {
#else
                    if (!headersDic.ContainsKey(currentKey))
                    {
                        headersDic.Add(currentKey, value);
                    }
                    else
                    {
#endif
                        Log.Warning("Header {Key} couldn't be added as argument to the waf", currentKey);
                    }
                }
            }

            return headersDic;
        }

        return null;
    }
}
