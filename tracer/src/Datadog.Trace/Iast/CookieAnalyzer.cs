// <copyright file="CookieAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Logging;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
#else
using System.Web;
#endif

namespace Datadog.Trace.Iast;

internal class CookieAnalyzer
{
    private static readonly Lazy<CookieAnalyzer> Instance = new Lazy<CookieAnalyzer>();
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CookieAnalyzer));
    private readonly Regex? _cookieFilterRegex = null;

    public CookieAnalyzer()
        : this(Iast.Instance.Settings)
    {
    }

    internal CookieAnalyzer(IastSettings settings)
        : this(settings.Enabled, settings.CookieFilterRegex, settings.RegexTimeout)
    {
    }

    internal CookieAnalyzer(bool iastEnabled, string pattern, double timeoutMilliSeconds)
    {
        if (iastEnabled && !string.IsNullOrEmpty(pattern))
        {
            var timeout = TimeSpan.FromMilliseconds(timeoutMilliSeconds);
            if (timeout.TotalMilliseconds == 0)
            {
                timeout = Regex.InfiniteMatchTimeout;
            }

            var options = RegexOptions.IgnoreCase | RegexOptions.Compiled;
            _cookieFilterRegex = new(pattern, options, timeout);
        }
    }

#if NETFRAMEWORK
    public static void AnalyzeCookies(HttpCookieCollection cookies, IntegrationId integrationId)
    {
        Instance.Value.AnalyzeCookieCollection(cookies, integrationId);
    }

    public void AnalyzeCookieCollection(HttpCookieCollection cookies, IntegrationId integrationId)
    {
        try
        {
            foreach (string cookieKey in cookies)
            {
                ReportVulnerabilities(integrationId, cookies[cookieKey]);
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(CookieAnalyzer)}.{nameof(AnalyzeCookies)}.net461 exception");
        }
    }

    private void ReportVulnerabilities(IntegrationId integrationId, HttpCookie cookie)
    {
        if (cookie.Values.Count == 0)
        {
            return;
        }

        var name = cookie.Name;
        var value = cookie.Values[0];

        // Insecure cookies with empty values are allowed, but not vulnerable
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        bool isFiltered = IsFiltered(name);

        string? samesiteCookie = cookie.Values["SameSite"];
        if (samesiteCookie?.Equals("Strict", System.StringComparison.InvariantCultureIgnoreCase) is not true)
        {
            IastModule.OnNoSamesiteCookie(integrationId, name, isFiltered);
        }

        if (!cookie.HttpOnly)
        {
            IastModule.OnNoHttpOnlyCookie(integrationId, name, isFiltered);
        }

        if (!cookie.Secure)
        {
            IastModule.OnInsecureCookie(integrationId, name, isFiltered);
        }
    }
#else
    // Extract the cookie information of a request from a IHeaderDictionary

    public static void AnalyzeCookies(IHeaderDictionary cookies, IntegrationId integrationId)
    {
        Instance.Value.AnalyzeCookieCollection(cookies, integrationId);
    }

    public void AnalyzeCookieCollection(IHeaderDictionary headers, IntegrationId integrationId)
    {
        try
        {
            if (!headers.TryGetValue(HeaderNames.SetCookie, out var cookieHeaderValues))
            {
                return;
            }

            foreach (var cookieHeaderValue in cookieHeaderValues)
            {
                AnalyzeCookie(cookieHeaderValue, integrationId);
            }
        }
        catch (Exception error)
        {
            Log.Error(error, $"{nameof(CookieAnalyzer)}.{nameof(AnalyzeCookies)}.netcore exception");
        }
    }

    private void AnalyzeCookie(string cookieHeaderValue, IntegrationId integrationId)
    {
        if (!IsExcluded(cookieHeaderValue))
        {
            if (SetCookieHeaderValue.TryParse(cookieHeaderValue, out var cookieHeader))
            {
                ReportVulnerabilities(integrationId, cookieHeader);
            }
        }
    }

    private void ReportVulnerabilities(IntegrationId integrationId, SetCookieHeaderValue cookie)
    {
        var name = cookie.Name.ToString();
        var value = cookie.Value.ToString();

        // Insecure cookies with empty values are allowed but not vulnerable
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        bool isFiltered = IsFiltered(name);

        if (cookie.SameSite != Microsoft.Net.Http.Headers.SameSiteMode.Strict)
        {
            IastModule.OnNoSamesiteCookie(integrationId, name, isFiltered);
        }

        if (!cookie.HttpOnly)
        {
            IastModule.OnNoHttpOnlyCookie(integrationId, name, isFiltered);
        }

        if (!cookie.Secure)
        {
            IastModule.OnInsecureCookie(integrationId, name, isFiltered);
        }
    }

    internal bool IsExcluded(string cookieName)
    {
        return string.IsNullOrWhiteSpace(cookieName) || cookieName.StartsWith(".AspNetCore.", StringComparison.OrdinalIgnoreCase);
    }
#endif

    internal bool IsFiltered(string cookieName)
    {
        try
        {
            return _cookieFilterRegex?.IsMatch(cookieName) ?? false;
        }
        catch (RegexMatchTimeoutException err)
        {
            IastModule.LogTimeoutError(err);
            return true;
        }
    }
}
