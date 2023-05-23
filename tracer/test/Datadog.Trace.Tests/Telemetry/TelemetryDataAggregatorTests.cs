// <copyright file="TelemetryDataAggregatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry;

public class TelemetryDataAggregatorTests
{
    [Fact]
    public void GetCombinedConfiguration_WhenHavePrevious_AndNoCurrent_ReturnsPrevious()
    {
        var previous = GetPopulatedTelemetryInput();
        var next = new TelemetryInput();
        var aggregator = new TelemetryDataAggregator(previous);

        var result = aggregator.Combine(next);

        result.Configuration.Should().BeSameAs(previous.Configuration);
        result.Dependencies.Should().BeSameAs(previous.Dependencies);
        result.Integrations.Should().BeSameAs(previous.Integrations);
        result.Metrics.Should().BeSameAs(previous.Metrics);
        result.Distributions.Should().BeSameAs(previous.Distributions);
        result.Products.Should().BeSameAs(previous.Products);
    }

    [Fact]
    public void GetCombinedConfiguration_WhenHaveCurrent_AndNoPrevious_ReturnsCurrent()
    {
        var next = GetPopulatedTelemetryInput();
        var aggregator = new TelemetryDataAggregator(null);

        var result = aggregator.Combine(next);

        result.Configuration.Should().BeSameAs(next.Configuration);
        result.Dependencies.Should().BeSameAs(next.Dependencies);
        result.Integrations.Should().BeSameAs(next.Integrations);
        result.Metrics.Should().BeSameAs(next.Metrics);
        result.Distributions.Should().BeSameAs(next.Distributions);
        result.Products.Should().BeSameAs(next.Products);
    }

    [Fact]
    public void GetCombinedConfiguration_WhenHaveCurrent_AndPrevious_CombinesValues()
    {
        var currentConfig = new ConfigurationKeyValue[] { new("previous", "value", "env_var", 1, TelemetryErrorCode.AppsecConfigurationError) };
        var previousConfig = new ConfigurationKeyValue[] { new("current", "value", "env_var", 1, TelemetryErrorCode.AppsecConfigurationError) };

        var currentDeps = new DependencyTelemetryData[] { new("current") };
        var previousDeps = new DependencyTelemetryData[] { new("previous") };

        var currentIntegrations = new IntegrationTelemetryData[] { new("current", false, true, null) };
        var previousIntegrations = new IntegrationTelemetryData[] { new("previous", false, true, null) };

        var currentProducts = new ProductsData { Appsec = new(true, null) };
        var previousProducts = new ProductsData { Profiler = new(true, null) };

        var previousMetrics = new List<MetricData> { new("tracer.start", new MetricSeries { new(1234, 5) }, true, "common") };
        var currentMetrics = new List<MetricData> { new("tracer.stop", new MetricSeries { new(123, 5) }, false, "common") };

        var previousDistributions = new List<DistributionMetricData> { new("span.serialize.ms", new() { 12.5, 354 }, false) };
        var currentDistributions = new List<DistributionMetricData> { new("span.send.ms", new() { 12.5, 354 }, false) };

        var current = new TelemetryInput(
            currentConfig,
            currentDeps,
            currentIntegrations,
            new MetricResults(currentMetrics, currentDistributions),
            currentProducts);

        var previous = new TelemetryInput(
            previousConfig,
            previousDeps,
            previousIntegrations,
            new MetricResults(previousMetrics, previousDistributions),
            previousProducts);

        var aggregator = new TelemetryDataAggregator(previous);

        var results = aggregator.Combine(current);

        results.Configuration
               .Should()
               .Contain(currentConfig)
               .And.Contain(previousConfig);
        results.Dependencies
               .Should()
               .Contain(currentDeps)
               .And.Contain(previousDeps);
        results.Integrations
               .Should()
               .Contain(currentIntegrations)
               .And.Contain(previousIntegrations);
        results.Metrics
               .Should()
               .Contain(currentMetrics)
               .And.Contain(previousMetrics);
        results.Distributions
               .Should()
               .Contain(currentDistributions)
               .And.Contain(previousDistributions);

        var products = results.Products;
        products.Should().NotBeNull();
        products.Appsec.Should().Be(currentProducts.Appsec);
        products.Profiler.Should().Be(previousProducts.Profiler);
    }

    [Fact]
    public void GetCombinedConfiguration_WhenHaveCurrent_AndPrevious_CombinesMetricPointsIntoSingleMetric()
    {
        const string metric = "tracer.start";
        const string distribution = "span.serialize.ms";
        var previousMetrics = new List<MetricData>
        {
            new(metric, new MetricSeries { new(1234, 5) }, true, "count"),
            new("previous.metric", new MetricSeries { new(1234, 5) }, true, "count"),
        };
        var currentMetrics = new List<MetricData>
        {
            new(metric, new MetricSeries { new(1245, 23) }, false, "count"),
            new("current.metric", new MetricSeries { new(1234, 5) }, true, "count"),
        };

        var previousDistributions = new List<DistributionMetricData>
        {
            new(distribution, new() { 1, 2.3 }, false),
            new("previous.dist", new() { 1, 2.3 }, false),
        };
        var currentDistributions = new List<DistributionMetricData>
        {
            new(distribution, new() { 3, 4 }, false),
            new("current.dist", new() { 3, 4 }, false),
        };

        var current = new TelemetryInput(
            null,
            null,
            null,
            new MetricResults(currentMetrics, currentDistributions),
            null);

        var previous = new TelemetryInput(
            null,
            null,
            null,
            new MetricResults(previousMetrics, previousDistributions),
            null);

        var aggregator = new TelemetryDataAggregator(previous);

        var results = aggregator.Combine(current);

        results.Configuration.Should().BeNull();
        results.Dependencies.Should().BeNull();
        results.Integrations.Should().BeNull();
        results.Products.Should().BeNull();

        var expectedMetrics = new[] { new { Metric = metric }, new { Metric = "previous.metric" }, new { Metric = "current.metric" }, };
        var metricValue = results.Metrics
                                 .Should()
                                 .BeEquivalentTo(expectedMetrics)
                                 .And.ContainSingle(x => x.Metric == metric)
                                 .Subject;

        metricValue.Points.Should().Contain(previousMetrics[0].Points);
        metricValue.Points.Should().Contain(currentMetrics[0].Points);

        var expectedDistributions = new[] { new { Metric = distribution }, new { Metric = "previous.dist" }, new { Metric = "current.dist" }, };
        var distributionValue = results.Distributions.Should()
                                       .BeEquivalentTo(expectedDistributions)
                                       .And.ContainSingle(x => x.Metric == distribution)
                                       .Subject;

        distributionValue.Points.Should().Contain(previousDistributions[0].Points);
        distributionValue.Points.Should().Contain(currentDistributions[0].Points);
    }

    [Fact]
    public void SaveDataIfRequired_TransientError_SavesData()
    {
        var aggregator = new TelemetryDataAggregator(previous: null);
        var result = TelemetryTransportResult.TransientError;
        var next = GetPopulatedTelemetryInput();

        aggregator.SaveDataIfRequired(result, in next);

        AssertStoredValues(aggregator, next);
    }

    [Theory]
    [InlineData((int)TelemetryTransportResult.Success)]
    [InlineData((int)TelemetryTransportResult.FatalError)]
    public void SaveDataIfRequired_SuccessOrFatal_DoesNotSaveData(int resultInt)
    {
        var aggregator = new TelemetryDataAggregator(previous: null);
        var result = (TelemetryTransportResult)resultInt;
        var next = GetPopulatedTelemetryInput();
        var expected = new TelemetryInput();

        aggregator.SaveDataIfRequired(result, next);

        AssertStoredValues(aggregator, expected);
    }

    [Fact]
    public void SaveDataIfRequired_OnSubsequentTransientError_ReplacesSavedData()
    {
        var aggregator = new TelemetryDataAggregator(previous: null);
        var result = TelemetryTransportResult.TransientError;
        var expected = GetPopulatedTelemetryInput();

        aggregator.SaveDataIfRequired(result, GetPopulatedTelemetryInput());
        aggregator.SaveDataIfRequired(result, expected);

        AssertStoredValues(aggregator, expected);
    }

    [Theory]
    [InlineData((int)TelemetryTransportResult.Success)]
    [InlineData((int)TelemetryTransportResult.FatalError)]
    public void SaveDataIfRequired_OnSubsequentSuccessOrFatal_ClearsSavedData(int resultInt)
    {
        var aggregator = new TelemetryDataAggregator(previous: null);
        var result = (TelemetryTransportResult)resultInt;

        aggregator.SaveDataIfRequired(TelemetryTransportResult.TransientError, GetPopulatedTelemetryInput());
        aggregator.SaveDataIfRequired(result, GetPopulatedTelemetryInput());

        AssertStoredValues(aggregator, new TelemetryInput());
    }

    private static TelemetryInput GetPopulatedTelemetryInput()
    {
        return new TelemetryInput(
            Array.Empty<ConfigurationKeyValue>(),
            Array.Empty<DependencyTelemetryData>(),
            Array.Empty<IntegrationTelemetryData>(),
            new MetricResults(new List<MetricData>(), new List<DistributionMetricData>()),
            new ProductsData());
    }

    private void AssertStoredValues(TelemetryDataAggregator aggregator, TelemetryInput expected)
    {
        var result = aggregator.Combine(new TelemetryInput());

        if (expected.Configuration is { } expectedConfig)
        {
            result.Configuration.Should().BeSameAs(expectedConfig);
        }
        else
        {
            result.Configuration.Should().BeNull();
        }

        if (expected.Dependencies is { } expectedDependencies)
        {
            result.Dependencies.Should().BeSameAs(expectedDependencies);
        }
        else
        {
            result.Dependencies.Should().BeNull();
        }

        if (expected.Integrations is { } expectedIntegrations)
        {
            result.Integrations.Should().BeSameAs(expectedIntegrations);
        }
        else
        {
            result.Integrations.Should().BeNull();
        }

        if (expected.Products is { } expectedProducts)
        {
            result.Products.Should().BeSameAs(expectedProducts);
        }
        else
        {
            result.Products.Should().BeNull();
        }

        if (expected.Metrics is { } expectedMetrics)
        {
            result.Metrics.Should().BeSameAs(expectedMetrics);
        }
        else
        {
            result.Metrics.Should().BeNull();
        }

        if (expected.Distributions is { } expectedDistributions)
        {
            result.Distributions.Should().BeSameAs(expectedDistributions);
        }
        else
        {
            result.Distributions.Should().BeNull();
        }
    }
}
