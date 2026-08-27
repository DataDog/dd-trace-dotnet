// <copyright file="TelemetryMetricExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Processors;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Metrics;

public class TelemetryMetricExtensionsTests
{
    private static readonly HashSet<string> DebuggerGuardrailCamelCaseTags =
    [
        "reason:rateLimitGlobal",
        "reason:rateLimitProbe",
        "reason:evaluationTimeout",
        "reason:queueFull",
        "reason:payloadTooLarge",
        "reason:runtimeError",
        "reason:fieldCount",
        "reason:collectionSize",
        "reason:stringLength",
    ];

    public static IEnumerable<object[]> AllEnums
        => GetEnums<Count>().Select(x => new object[] { x, x.GetName() })
          .Concat(GetEnums<CountShared>().Select(x => new object[] { x, x.GetName() }))
          .Concat(GetEnums<CountCIVisibility>().Select(x => new object[] { x, x.GetName() }))
          .Concat(GetEnums<Gauge>().Select(x => new object[] { x, x.GetName() }))
          .Concat(GetEnums<DistributionShared>().Select(x => new object[] { x, x.GetName() }))
          .Concat(GetEnums<DistributionCIVisibility>().Select(x => new object[] { x, x.GetName() }))
          .ToList();

    public static IEnumerable<object[]> IntegrationIds
        => IntegrationRegistry.Ids.Values.Select(x => new object[] { x });

    public static IEnumerable<object[]> WafReturnCodes
        => GetEnums<WafReturnCode>().Select(x => new object[] { (int)x });

    [Theory]
    [MemberData(nameof(AllEnums))]
    public void MustHaveMetricNameForAllValues(int api, string metricName)
    {
        _ = api;
        metricName.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(AllEnums))]
    public void MustHaveLowerCaseMetricNames(int api, string metricName)
    {
        _ = api;
        metricName.Should().Be(metricName.ToLowerInvariant());
    }

    [Theory]
    [MemberData(nameof(AllEnums))]
    public void MustHaveNormalizedMetricNames(int api, string metricName)
    {
        _ = api;
        metricName.Should().Be(TraceUtil.NormalizeMetricName(metricName, limit: 100));
    }

    [Fact]
    public void MustHaveUniqueNamesForAllMetrics()
    {
        AllEnums
           .Select(x => (string)x[1])
           .Should()
           .OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(IntegrationIds))]
    public void MustHaveMetricTagForAllIntegrations(int id)
    {
        var integrationId = (IntegrationId)id;
        var getTag = () => integrationId.GetMetricTag();
        getTag.Should().NotThrow("should have a mapping to a metric tag for every IntegrationId. Add a new entry to IntegrationIdExtensions");
    }

    [Fact]
    public void MustHaveUniqueMetricTagForAllIntegrations()
    {
        IntegrationIds
           .Select(x => ((IntegrationId)x[0]).GetMetricTag())
           .Should()
           .OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(WafReturnCodes))]
    public void MustHaveMetricTagForEveryWafErrorCode(int returnCode)
    {
        var tag = ((WafReturnCode)returnCode).ToWafErrorTag();

        if (returnCode < (int)WafReturnCode.Ok)
        {
            tag.Should().NotBeNull("every WAF error code should map to a metric tag. Add a new entry to WafReturnCodeExtensions");
            GetWafErrorCode(tag!.Value).Should().Be(returnCode, "the waf_error tag should report the ddwaf_run return code");
        }
        else
        {
            tag.Should().BeNull("only failed WAF runs are reported by appsec.waf.error");
        }
    }

    [Fact]
    public void MustHaveWafErrorCodeForEveryMetricTag()
    {
        var mapped = GetEnums<WafReturnCode>()
                    .Select(x => x.ToWafErrorTag())
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value);

        GetEnums<MetricTags.WafError>()
           .Except(new[] { MetricTags.WafError.BindingError })
           .Should()
           .BeSubsetOf(mapped, "every WAF error tag except the binding error should be reachable from a WafReturnCode");
    }

    [Fact]
    public void MustHaveValidTagsForEveryPublicApi()
    {
        foreach (var tag in GetEnums<PublicApiUsage>().Select(x => x.ToStringFast()))
        {
            AssertValidTags(new[] { tag }, allowDebuggerGuardrailTags: false);
        }
    }

    [Theory]
    [InlineData(typeof(MetricsTelemetryCollector), nameof(Count))]
    [InlineData(typeof(MetricsTelemetryCollector), nameof(CountShared))]
    [InlineData(typeof(MetricsTelemetryCollector), nameof(Gauge))]
    [InlineData(typeof(MetricsTelemetryCollector), nameof(DistributionShared))]
    [InlineData(typeof(CiVisibilityMetricsTelemetryCollector), nameof(CountShared))]
    [InlineData(typeof(CiVisibilityMetricsTelemetryCollector), nameof(CountCIVisibility))]
    [InlineData(typeof(CiVisibilityMetricsTelemetryCollector), nameof(DistributionShared))]
    [InlineData(typeof(CiVisibilityMetricsTelemetryCollector), nameof(DistributionCIVisibility))]
    public void MustHaveValidTagsForEveryMetric(Type collectorType, string enumType)
    {
        var keys = collectorType.GetMethod($"Get{enumType}Buffer", BindingFlags.Static | BindingFlags.NonPublic);
        var allowDebuggerGuardrailTags = collectorType == typeof(MetricsTelemetryCollector) && enumType == nameof(Count);
        CheckTagsAreValid(keys, allowDebuggerGuardrailTags);
    }

    private static void CheckTagsAreValid(MethodInfo getMetricKeys, bool allowDebuggerGuardrailTags)
    {
        var values = (Array)getMetricKeys.Invoke(null, Array.Empty<object>());
        for (var i = 0; i < values.Length; i++)
        {
            var duckTyped = values.GetValue(i).DuckCast<MetricKeyDuckType>();
            var tags = duckTyped.Tags;
            if (tags is null)
            {
                continue;
            }

            AssertValidTags(tags, allowDebuggerGuardrailTags);
        }
    }

    private static void AssertValidTags(string[] tags, bool allowDebuggerGuardrailTags)
        => tags.Should()
               .OnlyContain(x => x.ToLowerInvariant() == x || (allowDebuggerGuardrailTags && DebuggerGuardrailCamelCaseTags.Contains(x)), "should use lowercase unless the debugger contract requires camelCase")
               .And.OnlyContain(x => x.Trim() == x, "should not have any whitespace")
               .And.OnlyContain(x => TraceUtil.NormalizeTag(x) == x || (allowDebuggerGuardrailTags && DebuggerGuardrailCamelCaseTags.Contains(x)), "should match the normalized version unless the debugger contract requires camelCase");

    private static int GetWafErrorCode(MetricTags.WafError tag)
    {
        const string prefix = "waf_error:";
        var description = typeof(MetricTags.WafError)
                         .GetField(tag.ToString())!
                         .GetCustomAttribute<DescriptionAttribute>()!
                         .Description;

        var value = description.Split(';').Single(x => x.StartsWith(prefix, StringComparison.Ordinal));
        return int.Parse(value.Substring(prefix.Length), CultureInfo.InvariantCulture);
    }

    private static IEnumerable<T> GetEnums<T>()
        => Enum.GetValues(typeof(T)).Cast<T>();

    [DuckCopy]
    public struct MetricKeyDuckType
    {
        [DuckField]
        public string[] Tags;
    }
}
