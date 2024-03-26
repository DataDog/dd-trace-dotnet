// <copyright file="TelemetryMetricExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    [Fact]
    public void MustHaveValidTagsForEveryPublicApi()
    {
        foreach (var tag in GetEnums<PublicApiUsage>().Select(x => x.ToStringFast()))
        {
            AssertValidTags(new[] { tag });
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
        CheckTagsAreValid(keys);
    }

    private static void CheckTagsAreValid(MethodInfo getMetricKeys)
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

            AssertValidTags(tags);
        }
    }

    private static void AssertValidTags(string[] tags)
        => tags.Should()
               .OnlyContain(x => x.ToLowerInvariant() == x, "should all be lowercase")
               .And.OnlyContain(x => x.Trim() == x, "should not have any whitespace")
               .And.OnlyContain(x => TraceUtil.NormalizeTag(x) == x, "should match normalized version");

    private static IEnumerable<T> GetEnums<T>()
        => Enum.GetValues(typeof(T)).Cast<T>();

    [DuckCopy]
    public struct MetricKeyDuckType
    {
        [DuckField]
        public string[] Tags;
    }
}
