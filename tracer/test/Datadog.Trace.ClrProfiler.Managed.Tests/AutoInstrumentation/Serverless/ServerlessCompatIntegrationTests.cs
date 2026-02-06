// <copyright file="ServerlessCompatIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Serverless;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Serverless;

public class ServerlessCompatIntegrationTests
{
    [Fact]
    public void TracePipeName_WithPreGeneratedName_ReturnsTracerValue()
    {
        // Arrange
        ResetCachedPipeNames();
        const string preGeneratedName = "dd_trace_test123";
        const string compatLayerName = "dd_trace_compat456";
        SetExporterSettingsGeneratedNames(preGeneratedName, null);

        // Act
        var result = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
            null!,
            compatLayerName,
            null!,
            default);

        // Assert
        result.GetReturnValue().Should().Be(preGeneratedName);
    }

    [Fact]
    public void TracePipeName_WithoutPreGeneratedName_GeneratesUniqueName()
    {
        // Arrange
        ResetCachedPipeNames();
        const string compatLayerName = "dd_trace_compat456";
        SetExporterSettingsGeneratedNames(null, null);

        // Act
        var result = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
            null!,
            compatLayerName,
            null!,
            default);

        // Assert
        var pipeName = result.GetReturnValue();
        pipeName.Should().NotBe(compatLayerName);
        pipeName.Should().StartWith("dd_trace_");
        pipeName.Should().MatchRegex(@"^dd_trace_[a-f0-9]{32}$"); // base_guid format
    }

    [Fact]
    public void TracePipeName_CalledMultipleTimes_ReturnsCachedValue()
    {
        // Arrange
        ResetCachedPipeNames();
        SetExporterSettingsGeneratedNames(null, null);

        // Act
        var result1 = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
            null!,
            "compat_name_1",
            null!,
            default);

        var result2 = CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(
            null!,
            "compat_name_2",
            null!,
            default);

        // Assert
        result1.GetReturnValue().Should().Be(result2.GetReturnValue());
    }

    [Fact]
    public void DogStatsDPipeName_WithPreGeneratedName_ReturnsTracerValue()
    {
        // Arrange
        ResetCachedPipeNames();
        const string preGeneratedName = "dd_dogstatsd_test123";
        const string compatLayerName = "dd_dogstatsd_compat456";
        SetExporterSettingsGeneratedNames(null, preGeneratedName);

        // Act
        var result = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
            null!,
            compatLayerName,
            null!,
            default);

        // Assert
        result.GetReturnValue().Should().Be(preGeneratedName);
    }

    [Fact]
    public void DogStatsDPipeName_WithoutPreGeneratedName_GeneratesUniqueName()
    {
        // Arrange
        ResetCachedPipeNames();
        const string compatLayerName = "dd_dogstatsd_compat456";
        SetExporterSettingsGeneratedNames(null, null);

        // Act
        var result = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
            null!,
            compatLayerName,
            null!,
            default);

        // Assert
        var pipeName = result.GetReturnValue();
        pipeName.Should().NotBe(compatLayerName);
        pipeName.Should().StartWith("dd_dogstatsd_");
        pipeName.Should().MatchRegex(@"^dd_dogstatsd_[a-f0-9]{32}$"); // base_guid format
    }

    [Fact]
    public void DogStatsDPipeName_CalledMultipleTimes_ReturnsCachedValue()
    {
        // Arrange
        ResetCachedPipeNames();
        SetExporterSettingsGeneratedNames(null, null);

        // Act
        var result1 = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
            null!,
            "compat_name_1",
            null!,
            default);

        var result2 = CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(
            null!,
            "compat_name_2",
            null!,
            default);

        // Assert
        result1.GetReturnValue().Should().Be(result2.GetReturnValue());
    }

    [Theory]
    [InlineData("trace")]
    [InlineData("dogstatsd")]
    public void OnMethodEnd_WithException_PassesThroughOriginalValue(string pipeType)
    {
        // Arrange
        ResetCachedPipeNames();
        const string originalValue = "original_pipe_name";
        var exception = new InvalidOperationException("Test exception");

        // Act
        CallTargetReturn<string> result = pipeType == "trace"
            ? CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(null!, originalValue, exception, default)
            : CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(null!, originalValue, exception, default);

        // Assert
        result.GetReturnValue().Should().Be(originalValue);
    }

    [Theory]
    [InlineData("dd_trace", "trace")]
    [InlineData("dd_dogstatsd", "DogStatsD")]
    public void GeneratedPipeName_HasCorrectFormat(string baseName, string pipeType)
    {
        // Act
        var pipeName = ServerlessCompatPipeNameHelper.GenerateUniquePipeName(baseName, pipeType);

        // Assert
        pipeName.Should().StartWith($"{baseName}_");
        pipeName.Should().MatchRegex($@"^{baseName}_[a-f0-9]{{32}}$"); // base_guid with 32 hex chars
        pipeName.Length.Should().BeLessOrEqualTo(247); // 214 base + 1 underscore + 32 guid = 247 max
    }

    [Fact]
    public void GeneratedPipeName_WithLongBaseName_Truncates()
    {
        // Arrange - create a base name longer than 214 characters
        var longBaseName = new string('a', 220);

        // Act
        var pipeName = ServerlessCompatPipeNameHelper.GenerateUniquePipeName(longBaseName, "test");

        // Assert
        // Should be truncated to 214 + 1 underscore + 32 guid = 247 total
        pipeName.Length.Should().Be(247);
        pipeName.Should().MatchRegex(@"^a{214}_[a-f0-9]{32}$");
    }

    // Clear cached values between tests using reflection
    private static void ResetCachedPipeNames()
    {
        var traceIntegrationType = typeof(CompatibilityLayer_CalculateTracePipeName_Integration);
        var dogstatsdIntegrationType = typeof(CompatibilityLayer_CalculateDogStatsDPipeName_Integration);

        var traceCacheField = traceIntegrationType.GetField("_cachedTracePipeName", BindingFlags.NonPublic | BindingFlags.Static);
        var dogstatsdCacheField = dogstatsdIntegrationType.GetField("_cachedDogStatsDPipeName", BindingFlags.NonPublic | BindingFlags.Static);

        traceCacheField?.SetValue(null, null);
        dogstatsdCacheField?.SetValue(null, null);

        // Also clear ExporterSettings generated names
        var exporterSettingsType = typeof(ExporterSettings);
        var tracesPipeNameProp = exporterSettingsType.GetProperty("AzureFunctionsGeneratedTracesPipeName", BindingFlags.NonPublic | BindingFlags.Static);
        var metricsPipeNameProp = exporterSettingsType.GetProperty("AzureFunctionsGeneratedMetricsPipeName", BindingFlags.NonPublic | BindingFlags.Static);

        tracesPipeNameProp?.SetValue(null, null);
        metricsPipeNameProp?.SetValue(null, null);
    }

    private static void SetExporterSettingsGeneratedNames(string? tracePipeName, string? metricsPipeName)
    {
        var exporterSettingsType = typeof(ExporterSettings);
        var tracesPipeNameProp = exporterSettingsType.GetProperty("AzureFunctionsGeneratedTracesPipeName", BindingFlags.NonPublic | BindingFlags.Static);
        var metricsPipeNameProp = exporterSettingsType.GetProperty("AzureFunctionsGeneratedMetricsPipeName", BindingFlags.NonPublic | BindingFlags.Static);

        tracesPipeNameProp?.SetValue(null, tracePipeName);
        metricsPipeNameProp?.SetValue(null, metricsPipeName);
    }
}
#endif
