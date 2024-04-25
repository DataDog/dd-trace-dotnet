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
        where TTarget : IWebDriverProxy => PreClose(instance, parameters);

    internal static void OnPageClose<TTarget>(TTarget instance, Dictionary<string, object>? parameters)
        where TTarget : IWebDriverProxy => PreClose(instance, parameters);

    internal static void OnQuit<TTarget>(TTarget instance, Dictionary<string, object>? parameters)
        where TTarget : IWebDriverProxy => PreClose(instance, parameters);

    private static void PreClose<TTarget>(TTarget instance, Dictionary<string, object>? parameters)
        where TTarget : IWebDriverProxy
    {
        if (Interlocked.Read(ref _openPageCount) > 0)
        {
            CloseAndFlush(instance, parameters);
            Interlocked.Decrement(ref _openPageCount);
        }
    }

    internal static void OnAfterPageLoad<TTarget>(TTarget instance, Dictionary<string, object>? parameters)
        where TTarget : IWebDriverProxy
    {
        _seleniumCookieType ??= instance.Type.Assembly.GetType("OpenQA.Selenium.Cookie");
        if (_seleniumCookieType is not null)
        {
            var traceId = Test.Current?.GetInternalSpan().Context.TraceId ?? Tracer.Instance.ActiveScope?.Span?.Context.TraceId;
            if (traceId.HasValue && Activator.CreateInstance(_seleniumCookieType, "datadog-ci-visibility-test-execution-id", traceId.Value.ToString()) is { } cookieInstance)
            {
                Log.Debug("Inject: {Parameters}", JsonConvert.SerializeObject(parameters ?? new object()));
                instance.Manage().Cookies.AddCookie(cookieInstance);
                Interlocked.Increment(ref _openPageCount);
                if (Test.Current is { } test)
                {
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
                }
            }
        }
        else
        {
            Log.Warning("Could not find OpenQA.Selenium.Cookie type.");
        }
    }

    private static void CloseAndFlush<TTarget>(TTarget instance, Dictionary<string, object>? parameters)
        where TTarget : IWebDriverProxy
    {
        Log.Debug("CloseAndFlush");
        try
        {
            if (instance.ExecuteScript(RumStopSessionScript, null) is bool isRumFlushed)
            {
                if (Test.Current is { } test)
                {
                    test.SetTag("test.is_rum_active", isRumFlushed ? "true" : "false");
                }

                if (isRumFlushed)
                {
                    Log.Information<int>("RUM flush script has been called, waiting for {RumFlushWaitMillis}ms.", CIVisibility.Settings.RumFlushWaitMillis);
                    Thread.Sleep(CIVisibility.Settings.RumFlushWaitMillis);
                }
            }
        }
        catch (Exception ex)
        {
            if (Test.Current is { } test)
            {
                test.SetErrorInfo(ex);
            }

            Log.Error(ex, "Error running RUM flushing script.");
        }
    }
}
