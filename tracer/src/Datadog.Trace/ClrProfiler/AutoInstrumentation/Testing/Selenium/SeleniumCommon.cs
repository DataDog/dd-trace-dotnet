// <copyright file="SeleniumCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Selenium;

internal static class SeleniumCommon
{
    internal const string IntegrationName = nameof(IntegrationId.Selenium);
    private const IntegrationId IntegrationId = Configuration.IntegrationId.Selenium;

    internal const string CommandGet = "get";
    internal const string CommandClose = "close";
    internal const string CommandQuit = "quit";
    private const string CookieName = "datadog-ci-visibility-test-execution-id";
    private const string RumStopSessionScript = """
                                                if (window.DD_RUM && window.DD_RUM.stopSession) {
                                                   window.DD_RUM.stopSession();
                                                   return true;
                                                } else {
                                                   return false;
                                                }
                                                """;

    internal static readonly IDatadogLogger Log = CIVisibility.Log;
    private static Type? _seleniumCookieType;
    private static long _openPageCount;

    internal static bool IsEnabled => CIVisibility.IsRunning && Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId);

    internal static void OnBeforePageLoad<TTarget>(TTarget instance, Dictionary<string, object>? parameters)
        where TTarget : IWebDriverProxy => PreClose(instance);

    internal static void OnPageClose<TTarget>(TTarget instance)
        where TTarget : IWebDriverProxy => PreClose(instance);

    internal static void OnQuit<TTarget>(TTarget instance)
        where TTarget : IWebDriverProxy => PreClose(instance);

    private static void PreClose<TTarget>(TTarget instance)
        where TTarget : IWebDriverProxy
    {
        if (Interlocked.Read(ref _openPageCount) > 0)
        {
            CloseAndFlush(instance, Test.Current);
            Interlocked.Decrement(ref _openPageCount);
        }
    }

    internal static void OnAfterPageLoad<TTarget>(TTarget instance, Dictionary<string, object>? parameters)
        where TTarget : IWebDriverProxy
    {
        if (Test.Current is not { } test)
        {
            return;
        }

        _seleniumCookieType ??= instance.Type.Assembly.GetType("OpenQA.Selenium.Cookie");
        if (_seleniumCookieType is not null)
        {
            var span = test.GetInternalSpan();
            var tags = test.GetTags();
            var traceId = span.Context.TraceId;

            // Create a cookie with the traceId to be used by the RUM library
            if (Activator.CreateInstance(_seleniumCookieType, CookieName, traceId.ToString()) is { } cookieInstance)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Inject: {Parameters}", JsonConvert.SerializeObject(parameters ?? new object()));
                }

                // Inject cookie for RUM session
                instance.Manage().Cookies.AddCookie(cookieInstance);
                Interlocked.Increment(ref _openPageCount);

                // Tag the current test with browser information
                span.Type = SpanTypes.Browser;
                tags.BrowserDriver = "selenium";
                tags.BrowserDriverVersion = instance.Type.Assembly.GetName().Version?.ToString() ?? "unknown";

                var capabilities = instance.Capabilities;
                var browserName = capabilities.GetCapability("browserName")?.ToString() ?? "unknown";
                var browserVersion = (capabilities.GetCapability("browserVersion") ?? capabilities.GetCapability("version"))?.ToString() ?? string.Empty;
                if (tags.BrowserName is { } currentBrowserName && currentBrowserName != browserName)
                {
                    // According to the spec: If we have usage of different drivers in the same test, we set the browser name to empty
                    tags.BrowserName = string.Empty;
                }
                else
                {
                    tags.BrowserName = browserName;
                }

                if (tags.BrowserVersion is { } currentBrowserVersion && currentBrowserVersion != browserVersion)
                {
                    // According to the spec: If we have usage of different drivers in the same test, we set the browser version to empty
                    tags.BrowserVersion = string.Empty;
                }
                else
                {
                    tags.BrowserVersion = browserVersion;
                }

                // Add an action when the test close to flush the RUM data
                // in case the test never calls to driver.Close() or driver.Quit()
                // CloseAndFlush can be called multiple times.
                test.AddOnCloseAction(t => CloseAndFlush(instance, t));
            }
        }
        else
        {
            Log.Warning("Could not find OpenQA.Selenium.Cookie type.");
        }
    }

    private static void CloseAndFlush<TTarget>(TTarget instance, Test? test)
        where TTarget : IWebDriverProxy
    {
        if (test is null)
        {
            // Test is not available
            return;
        }

        if (instance.SessionId is null)
        {
            // The session is already closed (driver might be disposed)
            return;
        }

        var cookies = instance.Manage().Cookies;
        if (cookies.GetCookieNamed(CookieName) is null)
        {
            // Already closed don't need to close
            return;
        }

        Log.Debug("CloseAndFlush RUM session");
        try
        {
            // Execute RUM flush script
            if (instance.ExecuteScript(RumStopSessionScript, null) is true)
            {
                test.GetTags().IsRumActive = "true";
                Log.Information<int>("RUM flush script has been called, waiting for {RumFlushWaitMillis}ms.", CIVisibility.Settings.RumFlushWaitMillis);
                Thread.Sleep(CIVisibility.Settings.RumFlushWaitMillis);
            }

            // Delete injected RUM session cookie
            cookies.DeleteCookieNamed(CookieName);
        }
        catch (Exception ex)
        {
            test.SetErrorInfo(ex);
            Log.Error(ex, "Error running RUM flushing script.");
        }
    }
}
