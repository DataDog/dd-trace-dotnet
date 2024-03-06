// <copyright file="ApiSecurityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Security.Unit.Tests.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class ApiSecurityTests
{
    [Theory]
    [InlineData(true, true, SamplingPriority.UserKeep, true, "route0")]
    [InlineData(true, true, SamplingPriority.AutoKeep, true, "route1")]
    [InlineData(true, true, SamplingPriority.AutoReject, false, "route2")]
    [InlineData(true, false, SamplingPriority.AutoKeep, false, "route3")]
    [InlineData(false, false, SamplingPriority.AutoKeep, false, "route4")]
    [InlineData(true, true, SamplingPriority.AutoKeep, false, null)]
    public void ApiSecurityTest(bool enable, bool lastCall, SamplingPriority samplingPriority, bool expectedResult, string route)
    {
        var apiSec = new ApiSecurity(
            new SecuritySettings(
                new CustomSettingsForTests(
                    new Dictionary<string, object> { { Configuration.ConfigurationKeys.AppSec.ApiExperimentalSecurityEnabled, enable } }),
                new NullConfigurationTelemetry()));
        var dic = new Dictionary<string, object>();
        var span = new Span(new SpanContext(1, 1, samplingPriority), DateTimeOffset.Now);
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
        var apiSec = new ApiSecurity(new SecuritySettings(new CustomSettingsForTests(new Dictionary<string, object> { { Configuration.ConfigurationKeys.AppSec.ApiExperimentalSecurityEnabled, true } }), new NullConfigurationTelemetry()), maxRouteSize);
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
            var span = new Span(new SpanContext(1, 1, SamplingPriority.AutoKeep), dt);
            span.SetTag(Tags.HttpRoute, route);
            span.SetTag(Tags.HttpStatusCode, statusCode);
            span.SetTag(Tags.HttpMethod, method);
            var res = apiSec.ShouldAnalyzeSchema(true, span, dic, statusCode, new Dictionary<string, object>());
            res.Should().BeTrue();
        }

        var processedRoutes = apiSec.GetType().GetField("_processedRoutes", BindingFlags.Instance | BindingFlags.NonPublic);
        var innerDic = processedRoutes.GetValue(apiSec) as OrderedDictionary;
        innerDic.Count.Should().Be(maxRouteSize);
        var oldestElm = queue.First();
        var oldest = innerDic.Keys.Cast<int>().First();
        oldest.Should().Be(oldestElm);
    }
}
