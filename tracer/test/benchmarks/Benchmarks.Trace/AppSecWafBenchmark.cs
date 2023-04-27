// <copyright file="AppSecWafBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkAgent2]
public class AppSecWafBenchmark
{
    public const int TimeoutMicroSeconds = 1_000_000;

    private readonly Waf _waf;
    private Context _context;

    public AppSecWafBenchmark()
    {
        Environment.SetEnvironmentVariable("DD_INTERNAL_TRACE_NATIVE_ENGINE_PATH", "C:\\Repositories\\dd-trace-dotnet2\\shared\\bin\\monitoring-home\\win-x64\\");
        var libInitResult = WafLibraryInvoker.Initialize();
        if (!libInitResult.Success)
        {
            throw new ArgumentException("Waf could not load");
        }

        var wafLibraryInvoker = libInitResult.WafLibraryInvoker!;
        var initResult = Waf.Create(wafLibraryInvoker, string.Empty, string.Empty);
        _waf = initResult.Waf;
    }
    
    public IEnumerable<Dictionary<string, object>> Source() // for multiple arguments it's an IEnumerable of array of objects (object[])
    {
        yield return new Dictionary<string, object>
        {
            { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "something", ".htaccess" }, { "something2", ";shutdown--" } } },
            { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "[$ne]", "appscan_fingerprint" }, } },
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/" },
            { AddressesConstants.RequestMethod, "GET" },
        };
        yield return new Dictionary<string, object>
        {
            { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string> { { "x_filename", "routing.yml" } } },
            {
                AddressesConstants.RequestBody, new Dictionary<string, object>
                {
                    { "value1", "adsensepostnottherenonobook" },
                    { "value5", true },
                    { "value6", false },
                    { "value2", "security_scanner2" },
                    { "value3", "security_scanner3" },
                    { "value4", new Dictionary<string, object> { { "test1", "test2" }, { "test3", new List<string> { "test", "test2", "test3" } } } }
                }
            },
            { AddressesConstants.RequestPathParams, new Dictionary<string, object> { { "something", "appscan_fingerprint" }, { "something2", true }, { "something3", true }, { "something4", true }, { "something5", true }, { "so", new List<string> { "test", "test2", "test3", "test4" } } } },
            { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "something", ".htaccess" }, { "something2", ";shutdown--" } } },
            { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "[$ne]", "appscan_fingerprint" }, } },
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/" },
            { AddressesConstants.RequestMethod, "GET" },
        };
        yield return new Dictionary<string, object>
        {
            { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string> { { "x_filename", "routing.yml" }, { "x_filename2", "hello there very long string we have here now and it's potentially an attack" } } },
            { AddressesConstants.RequestBodyFileFieldNames, new Dictionary<string, string> { { "x_filename2", "routing.yml2" } } },
            {
                AddressesConstants.RequestBody, new Dictionary<string, object>
                {
                    { "value1", "adsensepostnottherenonobook" },
                    { "value5", true },
                    { "value6", false },
                    { "value2", "security_scanner2" },
                    { "value3", "security_scanner3" },
                    { "key", new Dictionary<string, object> { { "test1", "test2" }, { "test3", new List<string> { "test", "test2", "test3" } }, { "test4", new List<object> { true, true, true, false, true, 1234} } } },
                    { "value4", new Dictionary<string, object> { { "test1", "test2" }, { "test3", new List<string> { "test", "test2", "test3" } }, { "test4", new List<object> { true, true, true, false, true, 1234} } } }
                }
            },
            { AddressesConstants.RequestPathParams, new Dictionary<string, object> { { "something", "appscan_fingerprint" }, { "something2", true }, { "something3", true }, { "something4", true }, { "something5", true }, { "so", new List<string> { "test", "test2", "test3", "test4" } } } },
            { AddressesConstants.RequestCookies, new Dictionary<string, object> { { "something", ".htaccess" }, { "something2", ";shutdown--" }, {"test", new List<bool> { true, false, true, true, true } } } },
            { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "[$ne]", "appscan_fingerprint" }, { "arg!", "appscan_fingerprint" } } },
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/lalallala" },
            { AddressesConstants.RequestMethod, "POST" },
        };
    }
    
    [GlobalSetup]
    public void Setup()
    {
        _context = _waf.CreateContext() as Context;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [Benchmark]
    [ArgumentsSource(nameof(Source))]
    public void RunWafWithBindingsFromWaf(Dictionary<string, object> args)
    {
        _context.Run(args, TimeoutMicroSeconds);
    }

    [Benchmark]    
    [ArgumentsSource(nameof(Source))]
    public void RunWafWithMarshallEncoding(Dictionary<string, object> args)
    {
        _context.Run2(args, TimeoutMicroSeconds);
    }
}
