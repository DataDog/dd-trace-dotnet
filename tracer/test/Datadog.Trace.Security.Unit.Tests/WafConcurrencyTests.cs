// <copyright file="WafConcurrencyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#if NETFRAMEWORK
using System.Web.Routing;
#endif
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class WafConcurrencyTests : WafLibraryRequiredTest
{
    // here we use just 1 sec instead of the 20sec common one as we dont really care about the result, just that it runs
    public const int WafRunTimeoutMicroSeconds = 1_000_000;

    [Fact]
    public void MultipleWafOps()
    {
        var results = new ConcurrentBag<UpdateResult>();
        var threads = new List<Thread>();
        for (var i = 0; i < 5; i++)
        {
            var initResult = CreateWaf();
            var waf = initResult.Waf;
            waf.Should().NotBeNull();
            var confState = CreateConfigurationState("ruleset-withblockips.json");
            var disposeThread = new Thread(waf.Dispose);
            var updateThread = new Thread(
               () =>
               {
                   var ur = waf.Update(confState);
                   results.Add(ur);
               });

            updateThread.Start();
            disposeThread.Start();
            threads.Add(updateThread);
            threads.Add(disposeThread);
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        foreach (var result in results)
        {
            if (!result.Success)
            {
                var acceptableError = result.ErrorMessage.Contains("disposed") || result.ErrorMessage.Contains("Couldn't acquire lock to update waf");
                acceptableError.Should().BeTrue();
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MultipleContextsRun(bool useUnsafeEncoder)
    {
        if (useUnsafeEncoder)
        {
            AppSec.WafEncoding.Encoder.SetPoolSize(0);
        }

        var initResult = CreateWaf(useUnsafeEncoder);

        using var waf = initResult.Waf;
        var result = WafConfigurator.DeserializeEmbeddedOrStaticRules("remote-rules.json");
        result.Should().NotBeNull();
        var ruleSet = RuleSet.From(result!);
        ruleSet.Should().NotBeNull();

        var longArraylist = new ArrayList();
        for (var i = 0; i < 400; i++)
        {
            longArraylist.AddRange(new object[] { 1, 2, true, 3.0, 4.0, 5 });
        }

        const int updateThreads = 1;
        var threads = new Thread[20];

        // beware that the update thread is not thread safe, only ONE thread can update the waf at the time (should be ok as remote config works only on 1 thread)
        var threadsUpdate = new Thread[updateThreads];

        for (var t = 0; t < threads.Length; t++)
        {
            var thread = new Thread(
                () =>
                {
                    var args = CreateWafArgs();
                    var r = new Random();
                    for (var i = 0; i < 100; i++)
                    {
                        var next = r.Next();
                        using var context = waf.CreateContext();
                        if (context == null)
                        {
                            i--;
                            continue;
                        }

                        var result = context.Run(args, WafRunTimeoutMicroSeconds);
                        result.ReturnCode.Should().Be(WafReturnCode.Ok);
                        args.Clear();

                        args.Add(AddressesConstants.RequestBody, new List<string> { "dog1", "dog2", "dog3", "dog4" });
                        result = context.Run(args, WafRunTimeoutMicroSeconds);
                        result.Timeout.Should().BeFalse("Timeout should be false");
                        result.ReturnCode.Should().Be(WafReturnCode.Ok);
                        args.Clear();

                        args.Add(AddressesConstants.RequestCookies, new Dictionary<string, object> { { $"appscan_fingerprint{{j}}", "[$slice]" }, { "dog3", $"[$slice]{next}" } });
                        args.Add(AddressesConstants.RequestQuery, new Dictionary<string, object> { { $"appscan_fingerprint{{j}}", "[$slice]" }, { "dog3", "[$slice]" }, { "dog4", new[] { next, next + 1, next - 1 } } });
#if NETFRAMEWORK
                        args.Add(AddressesConstants.RequestPathParams, new RouteValueDictionary { { "controller", "Home" }, { "action", "Index" }, { "id", "appscan_fingerprint" } });
#else
                        args.Add(AddressesConstants.RequestPathParams, new Dictionary<string, object> { { "controller", "Home" }, { "action", "Index" }, { "id", "appscan_fingerprint" } });
#endif
                        result = context.Run(args, WafRunTimeoutMicroSeconds);
                        result.Timeout.Should().BeFalse("Timeout should be false");
                        args.Clear();

#if NETFRAMEWORK
                        args.Add(AddressesConstants.RequestPathParams, new RouteValueDictionary { { "id", "appscan_fingerprint" } });
#else
                        args.Add(AddressesConstants.RequestPathParams, new Dictionary<string, object> { { "id", "appscan_fingerprint" } });
#endif
                        result = context.Run(args, WafRunTimeoutMicroSeconds);
                        result.Timeout.Should().BeFalse("Timeout should be false");
                        args.Clear();

                        args.Add(AddressesConstants.ResponseBody, new List<object> { "dog1", true, 1.5, 1.40d, "dummy_rule", longArraylist, longArraylist });
                        args.Add(AddressesConstants.ResponseHeaderNoCookies, new Dictionary<string, ArrayList> { { "content-type", longArraylist } });
                        args.Add(AddressesConstants.ResponseStatus, "200");
                        result = context.Run(args, WafRunTimeoutMicroSeconds);
                        result.Timeout.Should().BeFalse("Timeout should be false");
                        result.ReturnCode.Should().Be(WafReturnCode.Ok);
                        args.Clear();
                    }
                });
            threads[t] = thread;

            if (t < updateThreads)
            {
                    var threadUpdate = new Thread(
                    () =>
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            var configurationState = UpdateConfigurationState(ruleSet: new() { { "test", ruleSet! } });
                            var res = waf!.Update(configurationState);
                            res.Success.Should().BeTrue();
                        }

                        Thread.Sleep(500);
                    });
                    threadsUpdate[t] = threadUpdate;
            }
        }

        var allThreads = new List<Thread>(threads);
        allThreads.AddRange(threadsUpdate);

        foreach (var thread in allThreads)
        {
            thread.Start();
        }

        foreach (var thread in allThreads)
        {
            thread.Join();
        }
    }

    [Fact]
    public void SimulateCrossThreadContextUse()
    {
        // context is shared between threads - and it's implementation should be thread safe
        // this tests attempts to ensure it is

        var initResult = CreateWaf();

        using var waf = initResult.Waf;
        var result = WafConfigurator.DeserializeEmbeddedOrStaticRules("remote-rules.json");
        result.Should().NotBeNull();
        var ruleSet = RuleSet.From(result!);
        ruleSet.Should().NotBeNull();

        var threads = new Thread[20];

        var context = waf.CreateContext();

        for (var t = 0; t < threads.Length; t++)
        {
            var thread = new Thread(
                () =>
                {
                    var args = CreateWafArgs();
                    try
                    {
                        for (var i = 0; i < 10; i++)
                        {
                            var result = context.Run(args, WafRunTimeoutMicroSeconds);
                            // we may not get a results if another thread has disposed the context
                            // this should be fine ...

                            // yield to try and maximize concurrent access during the test
                            Thread.Sleep(1);
                        }
                    }
                    finally
                    {
                        // it's import each thread attempt to dispose the context after use
                        // as this may happen during real usage
                        context.Dispose();
                    }
                });
            threads[t] = thread;
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }
    }

    private static Dictionary<string, object> CreateWafArgs()
    {
        var args = new Dictionary<string, object>
        {
            { AddressesConstants.WafContextProcessor, new Dictionary<string, bool> { { "extract-schema", false } } },
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/" },
            { AddressesConstants.ResponseStatus, "200" },
            { AddressesConstants.RequestQuery, new Dictionary<string, List<string>> { { "arg", new List<string> { "arg", "slice" } } } },
            { AddressesConstants.RequestMethod, "GET" },
            {
                AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string[]>
                {
                    { "accept-language", new[] { "en-US,en;q=0.9,es;q=0.8" } },
                    { "accept-encoding", new[] { "gzip, deflate, br" } },
                    { "sec-ch-ua", new[] { "\"Google Chrome\";v=\"119\", \"Chromium\";v=\"119\", \"Not?A_Brand\";v=\"24\"" } },
                    { "sec-fetch-site", new[] { "\"Google Chrome\";v=\"119\", \"Chromium\";v=\"119\", \"Not?A_Brand\";v=\"24\"" } },
                    { "sec-ch-ua-platform", new[] { "Windows" } },
                    { "sec-fetch-mode", new[] { "navigate" } },
                    { "sec-fetch-mode2", new[] { "navigate" } },
                    { "sec-fetch-dest", new[] { "document" } },
                    { "upgrade-insecure-requests", new[] { "1" } },
                    { "user-agent", new[] { "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36" } },
                    { "connection", new[] { "keep-alive" } },
                    { "host", new[] { "localhost:5458" } }
                }
            },
            { Tags.HttpClientIp, "127.0.0.1" },
        };
        return args;
    }
}
