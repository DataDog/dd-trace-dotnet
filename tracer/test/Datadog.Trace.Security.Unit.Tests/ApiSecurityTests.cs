// <copyright file="ApiSecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Security.Unit.Tests.Iast;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class ApiSecurityTests
{
    [Theory]
    [InlineData(true, true, SamplingPriorityValues.UserKeep, true, "route0")]
    [InlineData(true, true, SamplingPriorityValues.AutoKeep, true, "route1")]
    [InlineData(true, true, SamplingPriorityValues.AutoReject, false, "route2")]
    [InlineData(true, false, SamplingPriorityValues.AutoKeep, false, "route3")]
    [InlineData(false, false, SamplingPriorityValues.AutoKeep, false, "route4")]
    [InlineData(true, true, SamplingPriorityValues.AutoKeep, false, null)]
    public void ApiSecurityTest(bool enable, bool lastCall, int samplingPriority, bool expectedResult, string route)
    {
        var apiSec = new ApiSecurity(
            new SecuritySettings(
                new CustomSettingsForTests(
                    new Dictionary<string, object> { { Configuration.ConfigurationKeys.AppSec.ApiSecurityEnabled, enable } }),
                new NullConfigurationTelemetry()));
        var dic = new Dictionary<string, object>();
        var tc = new TraceContext(Mock.Of<IDatadogTracer>(), new TraceTagCollection());
        tc.SetSamplingPriority(samplingPriority);
        var span = new Span(new SpanContext(SpanContext.None, tc, "Test"), DateTimeOffset.Now);
        span.SetTag(Tags.HttpRoute, route);
        var statusCode = "200";
        span.SetTag(Tags.HttpStatusCode, statusCode);
        span.SetTag(Tags.HttpMethod, "GET");
        var res = apiSec.ShouldAnalyzeSchema(lastCall, span, dic, statusCode, new Dictionary<string, object>());
        if (res)
        {
            var res2 = apiSec.ShouldAnalyzeSchema(lastCall, span, dic, statusCode, new Dictionary<string, object>());
            // less than 30 sec for same route, statuscode, method
            res2.Should().Be(false);
        }

        res.Should().Be(expectedResult);
        if (expectedResult)
        {
            dic.Should().ContainKey(AddressesConstants.WafContextProcessor);
        }
    }

    [Fact]
    public void ApiSecurityTestMaxRoutes()
    {
        var maxRouteSize = 50;
        var apiSec = new ApiSecurity(new SecuritySettings(new CustomSettingsForTests(new Dictionary<string, object> { { Configuration.ConfigurationKeys.AppSec.ApiSecurityEnabled, true } }), new NullConfigurationTelemetry()), maxRouteSize);
        var queue = new Queue<int>(maxRouteSize);
        for (var i = 0; i < maxRouteSize + 1; i++)
        {
            var dt = DateTime.UtcNow;
            if (queue.Count == maxRouteSize)
            {
                queue.Dequeue();
            }

            var route = $"route{i}";
            var method = $"GET{i}";
            var statusCode = i.ToString();
            var resHash = ApiSecurity.CombineHashes(route, method, statusCode);
            queue.Enqueue(resHash);
            var dic = new Dictionary<string, object>();
            var tc = new TraceContext(Mock.Of<IDatadogTracer>(), new TraceTagCollection());
            tc.SetSamplingPriority(SamplingPriorityValues.AutoKeep);

            var span = new Span(new SpanContext(SpanContext.None, tc, "Test"), dt);
            span.SetTag(Tags.HttpRoute, route);
            span.SetTag(Tags.HttpStatusCode, statusCode);
            span.SetTag(Tags.HttpMethod, method);
            var res = apiSec.ShouldAnalyzeSchema(true, span, dic, statusCode, new Dictionary<string, object>());
            res.Should().BeTrue();
        }

        var innerDic = GetInnerOrderedDictionary(apiSec);
        innerDic.Count.Should().Be(maxRouteSize);
        var oldestElm = queue.First();
        var oldest = innerDic.Keys.Cast<int>().First();
        oldest.Should().Be(oldestElm);
    }

    [Fact]
    public void ApiSecurityTestMultiThread()
    {
        var apiSec = new ApiSecurity(
            new SecuritySettings(
                new CustomSettingsForTests(
                    new Dictionary<string, object> { { Configuration.ConfigurationKeys.AppSec.ApiSecurityEnabled, true }, { Configuration.ConfigurationKeys.AppSec.ApiSecuritySampleDelay, 120 } }),
                new NullConfigurationTelemetry()));
        var dic = new Dictionary<string, object> { { "controller", "test" }, { "action", "test" } };
        var tc = new TraceContext(Mock.Of<IDatadogTracer>(), new TraceTagCollection());
        tc.SetSamplingPriority(SamplingPriorityValues.AutoKeep);
        var dt = DateTime.UtcNow;

        var span = new Span(new SpanContext(SpanContext.None, tc, "Test"), dt);
        span.SetTag(Tags.HttpRoute, "{controller}/{action}");
        span.SetTag(Tags.HttpStatusCode, "200");
        span.SetTag(Tags.HttpMethod, "GET");
        List<Thread> threads = new();
        ConcurrentQueue<bool> results = new();
        for (var i = 0; i < 20; i++)
        {
            var thread = new Thread(
                o =>
                {
                    results.Enqueue(apiSec.ShouldAnalyzeSchema(true, span, dic, null, dic));
                });
            threads.Add(thread);
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        var innerDic = GetInnerOrderedDictionary(apiSec);
        innerDic.Count.Should().Be(1);
        results.Should().ContainSingle(p => p == true);
    }

    private static OrderedDictionary GetInnerOrderedDictionary(ApiSecurity apiSec)
    {
        var processedRoutes = apiSec.GetType().GetField("_processedRoutes", BindingFlags.Instance | BindingFlags.NonPublic);
        var innerDic = processedRoutes.GetValue(apiSec) as OrderedDictionary;
        return innerDic;
    }
}
