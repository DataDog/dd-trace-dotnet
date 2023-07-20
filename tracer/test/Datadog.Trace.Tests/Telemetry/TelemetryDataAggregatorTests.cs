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
        var aggregator = new TelemetryDataAggregator(previous);

        var result = aggregator.Combine(null, null, null, null, null);

        result.Configuration.Should().BeSameAs(previous.Configuration);
        result.Dependencies.Should().BeSameAs(previous.Dependencies);
        result.Integrations.Should().BeSameAs(previous.Integrations);
        result.Metrics.Should().BeNull(); // we don't store metrics
        result.Distributions.Should().BeNull(); // we don't store distributions
        result.Products.Should().BeSameAs(previous.Products);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetCombinedConfiguration_WhenHaveCurrent_AndNoPrevious_ReturnsCurrent(bool previousIsNull)
    {
        TelemetryInput? previous = previousIsNull ? null : new TelemetryInput();
        var aggregator = new TelemetryDataAggregator(previous);

        var next = GetPopulatedTelemetryInput();
        var result = aggregator.Combine(
            next.Configuration,
            next.Dependencies,
            next.Integrations,
            new MetricResults((List<MetricData>)next.Metrics, (List<DistributionMetricData>)next.Distributions),
            next.Products);

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

        var currentMetrics = new List<MetricData> { new("tracer.stop", new MetricSeries { new(123, 5) }, false, "common") };

        var currentDistributions = new List<DistributionMetricData> { new("span.send.ms", new() { 12.5, 354 }, false) };

        var previous = new TelemetryInput(
            previousConfig,
            previousDeps,
            previousIntegrations,
            null, // we don't save previous metrics and distributions
            previousProducts,
            sendAppStarted: false);

        var aggregator = new TelemetryDataAggregator(previous);

        var results = aggregator.Combine(
            currentConfig,
            currentDeps,
            currentIntegrations,
            new MetricResults(currentMetrics, currentDistributions),
            currentProducts);

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
               .Contain(currentMetrics);
        results.Distributions
               .Should()
               .Contain(currentDistributions);

        var products = results.Products;
        products.Should().NotBeNull();
        products.Appsec.Should().Be(currentProducts.Appsec);
        products.Profiler.Should().Be(previousProducts.Profiler);
    }

    [Fact]
    public void SaveDataIfRequired_OnError_SavesData()
    {
        var aggregator = new TelemetryDataAggregator(previous: null);
        var next = GetPopulatedTelemetryInput();

        aggregator.SaveDataIfRequired(success: false, in next);

        AssertStoredValues(aggregator, next);
    }

    [Fact]
    public void SaveDataIfRequired_Success_DoesNotSaveData()
    {
        var aggregator = new TelemetryDataAggregator(previous: null);
        var next = GetPopulatedTelemetryInput();
        var expected = new TelemetryInput();

        aggregator.SaveDataIfRequired(success: true, next);

        AssertStoredValues(aggregator, expected);
    }

    [Fact]
    public void SaveDataIfRequired_OnSubsequentTransientError_ReplacesSavedData()
    {
        var aggregator = new TelemetryDataAggregator(previous: null);
        var expected = GetPopulatedTelemetryInput();

        aggregator.SaveDataIfRequired(success: false, GetPopulatedTelemetryInput());
        aggregator.SaveDataIfRequired(success: false, expected);

        AssertStoredValues(aggregator, expected);
    }

    [Fact]
    public void SaveDataIfRequired_OnSubsequentSuccess_ClearsSavedData()
    {
        var aggregator = new TelemetryDataAggregator(previous: null);

        aggregator.SaveDataIfRequired(success: false, GetPopulatedTelemetryInput());
        aggregator.SaveDataIfRequired(success: true, GetPopulatedTelemetryInput());

        AssertStoredValues(aggregator, new TelemetryInput());
    }

    private static TelemetryInput GetPopulatedTelemetryInput()
    {
        return new TelemetryInput(
            Array.Empty<ConfigurationKeyValue>(),
            Array.Empty<DependencyTelemetryData>(),
            Array.Empty<IntegrationTelemetryData>(),
            new MetricResults(new List<MetricData>(), new List<DistributionMetricData>()),
            new ProductsData(),
            sendAppStarted: false);
    }

    private void AssertStoredValues(TelemetryDataAggregator aggregator, TelemetryInput expected)
    {
        var result = aggregator.Combine(null, null, null, null, null);

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

        // We don't store metrics or distributions
        result.Metrics.Should().BeNull();
        result.Distributions.Should().BeNull();
    }
}
