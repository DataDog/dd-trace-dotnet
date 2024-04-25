// <copyright file="SeleniumCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

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
            var traceId = test.GetInternalSpan().Context.TraceId;
            if (Activator.CreateInstance(_seleniumCookieType, CookieName, traceId.ToString()) is { } cookieInstance)
            {
                Log.Debug("Inject: {Parameters}", JsonConvert.SerializeObject(parameters ?? new object()));

                // Inject cookie for RUM session
                instance.Manage().Cookies.AddCookie(cookieInstance);
                Interlocked.Increment(ref _openPageCount);

                // Tag the current test with browser information
                test.GetInternalSpan().Type = SpanTypes.Browser;
                test.SetTag("test.browser.driver", "selenium");
                test.SetTag("test.browser.driver_version", instance.Type.Assembly.GetName().Version?.ToString() ?? "unknown");

                var browserName = instance.Instance?.GetType().Name switch
                {
                    "ChromeDriver" => "Chrome",
                    "ChromiumDriver" => "Chromium",
                    "EdgeDriver" => "Edge",
                    "FirefoxDriver" => "Firefox",
                    "InternetExplorerDriver" => "Internet Explorer",
                    "SafariDriver" => "Safari",
                    _ => "Unknown"
                };
                test.SetTag("test.browser.name", browserName);
                test.SetTag("test.browser.version", string.Empty);

                // Add an action when the test close to flush the RUM data
                // in case the test never calls to driver.Close() or driver.Quit()
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

        if (test.GetInternalSpan().GetTag("test.is_rum_active") is not null)
        {
            // Already closed don't need to close
            return;
        }

        if (instance.SessionId is null)
        {
            // The session is already closed (driver might be disposed)
            return;
        }

        Log.Debug("CloseAndFlush RUM session");
        try
        {
            if (instance.ExecuteScript(RumStopSessionScript, null) is bool isRumFlushed)
            {
                test.SetTag("test.is_rum_active", isRumFlushed ? "true" : "false");
                if (isRumFlushed)
                {
                    Log.Information<int>("RUM flush script has been called, waiting for {RumFlushWaitMillis}ms.", CIVisibility.Settings.RumFlushWaitMillis);
                    Thread.Sleep(CIVisibility.Settings.RumFlushWaitMillis);
                }
            }

            // Delete injected RUM session cookie
            instance.Manage().Cookies.DeleteCookieNamed(CookieName);
        }
        catch (Exception ex)
        {
            test.SetErrorInfo(ex);
            Log.Error(ex, "Error running RUM flushing script.");
        }
    }
}
