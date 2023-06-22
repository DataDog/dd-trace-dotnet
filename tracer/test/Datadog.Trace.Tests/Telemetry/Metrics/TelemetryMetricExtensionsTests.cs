﻿// <copyright file="TelemetryMetricExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Metrics;

public class TelemetryMetricExtensionsTests
{
    public static IEnumerable<object[]> AllEnums
        => GetEnums<PublicApiUsage>()
          .Select(x => new object[] { x, x.ToStringFast() })
          .Concat(GetEnums<Count>().Select(x => new object[] { x, x.GetName() }))
          .Concat(GetEnums<Gauge>().Select(x => new object[] { x, x.GetName() }))
          .Concat(GetEnums<Distribution>().Select(x => new object[] { x, x.GetName() }))
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

    private static IEnumerable<T> GetEnums<T>()
        => Enum.GetValues(typeof(T)).Cast<T>();
}
