// <copyright file="AppSecWafBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;

namespace Benchmarks.Trace.Asm;

[MemoryDiagnoser]
[BenchmarkAgent7]
[BenchmarkCategory(Constants.AppSecCategory)]
[IgnoreProfile]
public class AppSecWafBenchmark
{
    private const int TimeoutMicroSeconds = 1_000_000;

    private Waf _waf;
    private Dictionary<string, object> _stage1;
    private Dictionary<string, object> _stage1Attack;
    private Dictionary<string, object> _stage2;
    private Dictionary<string, object> _stage3;

    [GlobalSetup]
    public void GlobalSetup()
    {
        AppSecBenchmarkUtils.SetupDummyAgent();
        var wafLibraryInvoker = AppSecBenchmarkUtils.CreateWafLibraryInvoker();

        var rulesPath = Path.Combine(Directory.GetCurrentDirectory(), "Asm", "rule-set.1.10.0.json");
        var config = new NameValueCollection
        {
            { ConfigurationKeys.AppSec.Rules, rulesPath },
            { ConfigurationKeys.AppSec.Enabled, "1" }
        };
        var configSource = new NameValueConfigurationSource(config);
        var settings = new SecuritySettings(configSource, NullConfigurationTelemetry.Instance);
        var initResult = Waf.Create(wafLibraryInvoker, string.Empty, string.Empty, new Datadog.Trace.AppSec.Rcm.ConfigurationState(settings, null, true));
        if (!initResult.Success || initResult.HasErrors)
        {
            throw new ArgumentException($"Waf could not initialize, error message is: {initResult.ErrorMessage}");
        }
        _waf = initResult.Waf;

        _stage1 = MakeRealisticNestedMapStage1(false);
        _stage1Attack = MakeRealisticNestedMapStage1(true);
        _stage2 = MakeRealisticNestedMapStage2();
        _stage3 = MakeRealisticNestedMapStage3();

        // More aggressive warmup for native code paths (WAF library)
        // Ensures JIT compilation completes and native context creation stabilizes
        for (int i = 0; i < 10; i++)
        {
            RunWafRealisticBenchmark();
            RunWafRealisticBenchmarkWithAttack();
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Force GC to reduce variance from native memory interactions
        // WAF library uses unmanaged memory with allocation patterns outside .NET GC control
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static Dictionary<string, object> MakeRealisticNestedMapStage1(bool withAttack)
    {
        var headersDict = new Dictionary<string, string[]>
        {
            { "header1", new [] { "value1", "value2", "value3", } },
            { "header2", new [] { "value1", "value2", "value3", } },
            { "header3", new [] { "value1", "value2", "value3", } },
        };
        var cookiesDic = new Dictionary<string, List<string>>
        {
            { "cookie1", new List<string> { "value1", "value2", "value3", } },
        };

        if (withAttack)
        {
            cookiesDic.Add("user-agent", new List<string>() { "Arachni/v1" });
        }

        var queryStringDic = new Dictionary<string, List<string>>
        {
            { "key1", new List<string> { "value1", "value2", "value3", } },
        };

        var addressesDictionary = new Dictionary<string, object>
        {
            { AddressesConstants.RequestMethod, "GET" },
            { AddressesConstants.ResponseStatus, "200" },
            { AddressesConstants.RequestUriRaw, "/short/url" },
            { AddressesConstants.RequestClientIp, "10.20.30.40" }
        };

        addressesDictionary.Add(AddressesConstants.RequestQuery, queryStringDic);
        addressesDictionary.Add(AddressesConstants.RequestHeaderNoCookies, headersDict);
        addressesDictionary.Add(AddressesConstants.RequestCookies, cookiesDic);

        return addressesDictionary;
    }

    private static Dictionary<string, object> MakeRealisticNestedMapStage2()
    {
        IDictionary<string, object> pathParams = new Dictionary<string, object>()
        {
            { "id", "22" }
        };

        var args = new Dictionary<string, object>
        {
            { AddressesConstants.RequestPathParams, pathParams }
        };

        return args;
    }

    private static Dictionary<string, object> MakeRealisticNestedMapStage3()
    {
        var headersDict = new Dictionary<string, string[]>()
        {
            { "header1", new [] { "value1", "value2", "value3", } },
            { "header2", new [] { "value1", "value2", "value3", } },
            { "header3", new [] { "value1", "value2", "value3", } },
        };

        var args = new Dictionary<string, object>
        {
            {
                AddressesConstants.ResponseHeaderNoCookies,
                headersDict
            },
            { AddressesConstants.ResponseStatus, "200" },
        };

        return args;
    }

    [Benchmark]
    public void RunWafRealisticBenchmark()
    {
        var context = _waf.CreateContext();
        context!.Run(_stage1, TimeoutMicroSeconds);
        context!.Run(_stage2, TimeoutMicroSeconds);
        context!.Run(_stage3, TimeoutMicroSeconds);
        context.Dispose();
    }

    [Benchmark]
    public void RunWafRealisticBenchmarkWithAttack()
    {
        var context = _waf.CreateContext();
        context!.Run(_stage1Attack, TimeoutMicroSeconds);
        context.Dispose();
    }
}
