// <copyright file="DynamicAtrRetriesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(TracerInstanceTestCollection))]
[EnvironmentVariablesCleaner(
    ConfigurationKeys.CIVisibility.DynamicAtrEnabled,
    ConfigurationKeys.CIVisibility.DynamicAtrBuckets,
    ConfigurationKeys.CIVisibility.FlakyRetryEnabled,
    ConfigurationKeys.CIVisibility.FlakyRetryCount)]
public class DynamicAtrRetriesTests : SettingsTestsBase
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    [InlineData("1", true)]
    public void DynamicAtrEnablementParsing(string? enabled, bool expected)
    {
        var envVars = new List<(string Key, string Value)>();
        if (enabled is not null)
        {
            envVars.Add((ConfigurationKeys.CIVisibility.DynamicAtrEnabled, enabled));
        }

        var settings = CreateSettings(envVars.ToArray());
        settings.DynamicAtrEnabled.Should().Be(expected);
    }

    public static IEnumerable<object[]> DynamicAtrBucketsTestData => new List<object[]>
    {
        new object[] { null, null },
        new object[] { "", null },
        new object[] { "10,4,1,1,1", new[] { 10, 4, 1, 1, 1 } },
        new object[] { "10,4,1", null },             // wrong count
        new object[] { "10,4,0,1,1", null },         // value < 1
        new object[] { "21,4,1,1,1", null },        // value > 20
        new object[] { "invalid", null },            // non-integer
        new object[] { "a,b,c,d,e", null },          // all non-integer
    };

    [Theory]
    [MemberData(nameof(DynamicAtrBucketsTestData))]
    public void DynamicAtrBucketsParsing(string? buckets, int[]? expected)
    {
        var envVars = new List<(string Key, string Value)>();
        if (buckets is not null)
        {
            envVars.Add((ConfigurationKeys.CIVisibility.DynamicAtrBuckets, buckets));
        }

        var settings = CreateSettings(envVars.ToArray());
        if (expected is null)
        {
            settings.DynamicAtrBuckets.Should().BeNull();
        }
        else
        {
            settings.DynamicAtrBuckets.Should().Equal(expected);
        }
    }

    [Fact]
    public void SlowTestRetries_RetryBucketIndexForDuration()
    {
        var slowRetries = new TestOptimizationClient.SlowTestRetriesSettingsResponse(10, 5, 3, 1);
        slowRetries.RetryBucketIndexForDuration(1).Should().Be(0);
        slowRetries.RetryBucketIndexForDuration(5).Should().Be(0);
        slowRetries.RetryBucketIndexForDuration(6).Should().Be(1);
        slowRetries.RetryBucketIndexForDuration(10).Should().Be(1);
        slowRetries.RetryBucketIndexForDuration(11).Should().Be(2);
        slowRetries.RetryBucketIndexForDuration(30).Should().Be(2);
        slowRetries.RetryBucketIndexForDuration(31).Should().Be(3);
        slowRetries.RetryBucketIndexForDuration(300).Should().Be(3);
        slowRetries.RetryBucketIndexForDuration(301).Should().Be(4);
    }

    [Fact]
    public void SlowTestRetries_RetriesForDuration_UsesEfdSettings()
    {
        var slowRetries = new TestOptimizationClient.SlowTestRetriesSettingsResponse(10, 5, 3, 1);
        slowRetries.RetriesForDuration(1).Should().Be(10);    // 5s bucket
        slowRetries.RetriesForDuration(6).Should().Be(5);     // 10s bucket
        slowRetries.RetriesForDuration(11).Should().Be(3);    // 30s bucket
        slowRetries.RetriesForDuration(31).Should().Be(1);    // 5m bucket
        slowRetries.RetriesForDuration(301).Should().Be(0);   // >5m bucket
    }

    [Fact]
    public void SlowTestRetries_RetriesForDuration_NullFieldsDefaultToZero()
    {
        var slowRetries = new TestOptimizationClient.SlowTestRetriesSettingsResponse();
        slowRetries.RetriesForDuration(1).Should().Be(0);
        slowRetries.RetriesForDuration(301).Should().Be(0);
    }

    [Fact]
    public void ParseDynamicAtrBuckets_ValidInput_ReturnsBuckets()
    {
        TestOptimizationSettings.ParseDynamicAtrBuckets("10,4,1,1,1")
            .Should().Equal(new[] { 10, 4, 1, 1, 1 });
    }

    [Fact]
    public void ParseDynamicAtrBuckets_NullOrEmpty_ReturnsNull()
    {
        TestOptimizationSettings.ParseDynamicAtrBuckets(null).Should().BeNull();
        TestOptimizationSettings.ParseDynamicAtrBuckets("").Should().BeNull();
    }

    [Fact]
    public void ParseDynamicAtrBuckets_WrongCount_ReturnsNull()
    {
        TestOptimizationSettings.ParseDynamicAtrBuckets("10,4,1").Should().BeNull();
        TestOptimizationSettings.ParseDynamicAtrBuckets("10,4,1,1,1,1").Should().BeNull();
    }

    [Fact]
    public void ParseDynamicAtrBuckets_ValueOutOfRange_ReturnsNull()
    {
        TestOptimizationSettings.ParseDynamicAtrBuckets("0,4,1,1,1").Should().BeNull();
        TestOptimizationSettings.ParseDynamicAtrBuckets("21,4,1,1,1").Should().BeNull();
    }

    [Fact]
    public void ParseDynamicAtrBuckets_NonInteger_ReturnsNull()
    {
        TestOptimizationSettings.ParseDynamicAtrBuckets("a,b,c,d,e").Should().BeNull();
        TestOptimizationSettings.ParseDynamicAtrBuckets("10,4,1,1,x").Should().BeNull();
    }

    [Fact]
    public void DynamicAtrEnabled_DoesNotAffectFlakyRetryCount()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "true"),
            (ConfigurationKeys.CIVisibility.FlakyRetryCount, "5"));
        settings.DynamicAtrEnabled.Should().BeTrue();
        settings.FlakyRetryCount.Should().Be(5);
    }

    [Fact]
    public void DynamicAtrBuckets_OnlyTakesEffectWhenEnabled()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "false"),
            (ConfigurationKeys.CIVisibility.DynamicAtrBuckets, "3,1,1,1,1"));
        settings.DynamicAtrEnabled.Should().BeFalse();
        settings.DynamicAtrBuckets.Should().Equal(new[] { 3, 1, 1, 1, 1 });
    }

    [Fact]
    public void Telemetry_DynamicAtrRetriesMetric_HasCustomBucketsTrue()
    {
        var mockCollector = new Mock<IMetricsTelemetryCollector>();
        var original = TelemetryFactory.SetMetricsForTesting(mockCollector.Object);
        try
        {
            var settings = CreateSettings(
                (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "true"),
                (ConfigurationKeys.CIVisibility.DynamicAtrBuckets, "3,1,1,1,1"),
                (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"));

            var flakyRetryFeature = CreateFlakyRetryFeature(settings, backendEnabled: true);

            TestOptimization.RecordDynamicAtrTelemetry(settings, flakyRetryFeature);

            mockCollector.Verify(
                c => c.RecordCountCIVisibilityDynamicAtrRetries(
                    MetricTags.CIVisibilityDynamicAtrRetriesHasCustomBuckets.True,
                    It.IsAny<int>()),
                Times.Once);
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(original);
        }
    }

    [Fact]
    public void Telemetry_DynamicAtrRetriesMetric_HasCustomBucketsFalse()
    {
        var mockCollector = new Mock<IMetricsTelemetryCollector>();
        var original = TelemetryFactory.SetMetricsForTesting(mockCollector.Object);
        try
        {
            var settings = CreateSettings(
                (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "true"),
                (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"));

            var flakyRetryFeature = CreateFlakyRetryFeature(settings, backendEnabled: true);

            TestOptimization.RecordDynamicAtrTelemetry(settings, flakyRetryFeature);

            mockCollector.Verify(
                c => c.RecordCountCIVisibilityDynamicAtrRetries(
                    MetricTags.CIVisibilityDynamicAtrRetriesHasCustomBuckets.False,
                    It.IsAny<int>()),
                Times.Once);
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(original);
        }
    }

    [Fact]
    public void Telemetry_DynamicAtrDisabled_DoesNotRecordMetric()
    {
        var mockCollector = new Mock<IMetricsTelemetryCollector>();
        var original = TelemetryFactory.SetMetricsForTesting(mockCollector.Object);
        try
        {
            var settings = CreateSettings(
                (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "false"),
                (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"));
            var flakyRetryFeature = CreateFlakyRetryFeature(settings, backendEnabled: true);

            TestOptimization.RecordDynamicAtrTelemetry(settings, flakyRetryFeature);

            mockCollector.Verify(
                c => c.RecordCountCIVisibilityDynamicAtrRetries(
                    It.IsAny<MetricTags.CIVisibilityDynamicAtrRetriesHasCustomBuckets>(),
                    It.IsAny<int>()),
                Times.Never);
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(original);
        }
    }

    [Fact]
    public void SkippedSettingsRequestWithLocalFlatAtrDoesNotEnableDynamicAtr()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.CodeCoverage, "false"),
            (ConfigurationKeys.CIVisibility.TestsSkippingEnabled, "false"),
            (ConfigurationKeys.CIVisibility.EarlyFlakeDetectionEnabled, "false"),
            (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"),
            (ConfigurationKeys.CIVisibility.FlakyRetryCount, "5"),
            (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "true"),
            (ConfigurationKeys.CIVisibility.DynamicInstrumentationEnabled, "false"),
            (ConfigurationKeys.CIVisibility.ImpactedTestsDetectionEnabled, "false"),
            (ConfigurationKeys.CIVisibility.TestManagementEnabled, "false"));
        var locallySynthesizedResponse = TestOptimizationClient.CreateSettingsResponseFromTestOptimizationSettings(settings, tracerManagement: null);
        var flakyRetryFeature = TestOptimizationFlakyRetryFeature.Create(settings, locallySynthesizedResponse, isRemoteSettingsResponse: false);
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.SetupGet(x => x.Settings).Returns(settings);
        testOptimization.SetupGet(x => x.FlakyRetryFeature).Returns(flakyRetryFeature);
        var metadata = new TestCaseMetadata("case", totalExecution: 1, countDownExecutionNumber: 0)
        {
            SelectedRetryMode = TestRetryMode.AutomaticTestRetry,
        };

        locallySynthesizedResponse.FlakyTestRetries.Should().BeTrue();
        flakyRetryFeature.Enabled.Should().BeTrue();
        flakyRetryFeature.BackendEnabled.Should().BeFalse();
        flakyRetryFeature.DynamicAtrEnabled.Should().BeFalse();

        XUnitIntegration.InitializeTotalExecutions(testOptimization.Object, metadata, TimeSpan.FromSeconds(1));

        metadata.TotalExecutions.Should().Be(6);

        var mockCollector = new Mock<IMetricsTelemetryCollector>();
        var original = TelemetryFactory.SetMetricsForTesting(mockCollector.Object);
        try
        {
            TestOptimization.RecordDynamicAtrTelemetry(settings, flakyRetryFeature);

            mockCollector.Verify(
                c => c.RecordCountCIVisibilityDynamicAtrRetries(
                    It.IsAny<MetricTags.CIVisibilityDynamicAtrRetriesHasCustomBuckets>(),
                    It.IsAny<int>()),
                Times.Never);
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(original);
        }
    }

    [Fact]
    public void DynamicAtrRequiresBackendAtrDespiteLocalFlatAtrOverride()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "true"),
            (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"),
            (ConfigurationKeys.CIVisibility.FlakyRetryCount, "5"));

        var flakyRetryFeature = CreateFlakyRetryFeature(settings, backendEnabled: false);

        flakyRetryFeature.Enabled.Should().BeTrue();
        flakyRetryFeature.BackendEnabled.Should().BeFalse();
        flakyRetryFeature.DynamicAtrEnabled.Should().BeFalse();
    }

    [Fact]
    public void XUnit_DynamicAtrFallbackSchedulesOneRetryForDurationsOverFiveMinutes()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "true"),
            (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"));
        var flakyRetryFeature = CreateFlakyRetryFeature(settings, backendEnabled: true);
        var earlyFlakeDetectionFeature = new Mock<ITestOptimizationEarlyFlakeDetectionFeature>();
        earlyFlakeDetectionFeature.SetupGet(x => x.EarlyFlakeDetectionSettings).Returns(
            new TestOptimizationClient.EarlyFlakeDetectionSettingsResponse(
                enabled: false,
                slowTestRetries: new TestOptimizationClient.SlowTestRetriesSettingsResponse(),
                faultySessionThreshold: 0));
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.SetupGet(x => x.Settings).Returns(settings);
        testOptimization.SetupGet(x => x.FlakyRetryFeature).Returns(flakyRetryFeature);
        testOptimization.SetupGet(x => x.EarlyFlakeDetectionFeature).Returns(earlyFlakeDetectionFeature.Object);
        var metadata = new TestCaseMetadata("case", totalExecution: 1, countDownExecutionNumber: 0)
        {
            SelectedRetryMode = TestRetryMode.AutomaticTestRetry,
        };
        var originalTestOptimization = TestOptimization.Instance;
        try
        {
            TestOptimization.Instance = testOptimization.Object;

            XUnitIntegration.InitializeTotalExecutions(testOptimization.Object, metadata, TimeSpan.FromSeconds(301));

            metadata.TotalExecutions.Should().Be(2);
            var remainingRetries = 1;
            XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures: true, hasNotRun: false, ref remainingRetries)
                            .Should().Be(XUnitRetryExecutionDecision.Retry);
            remainingRetries.Should().Be(0);
            XUnitIntegration.GetRetryExecutionDecision(metadata, hasFailures: true, hasNotRun: false, ref remainingRetries)
                            .Should().Be(XUnitRetryExecutionDecision.RetryBudgetExhausted);
        }
        finally
        {
            TestOptimization.Instance = originalTestOptimization;
        }
    }

    [Fact]
    public void XUnit_UsesFlatRetryBudgetWhenBackendAtrIsDisabled()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "true"),
            (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"),
            (ConfigurationKeys.CIVisibility.FlakyRetryCount, "5"));
        var flakyRetryFeature = CreateFlakyRetryFeature(settings, backendEnabled: false);
        var testOptimization = new Mock<ITestOptimization>();
        testOptimization.SetupGet(x => x.Settings).Returns(settings);
        testOptimization.SetupGet(x => x.FlakyRetryFeature).Returns(flakyRetryFeature);
        var metadata = new TestCaseMetadata("case", totalExecution: 1, countDownExecutionNumber: 0)
        {
            SelectedRetryMode = TestRetryMode.AutomaticTestRetry,
        };

        XUnitIntegration.InitializeTotalExecutions(testOptimization.Object, metadata, TimeSpan.FromSeconds(1));

        metadata.TotalExecutions.Should().Be(6);
    }

    [Fact]
    public void Telemetry_BackendAtrDisabled_DoesNotRecordDynamicAtrMetric()
    {
        var mockCollector = new Mock<IMetricsTelemetryCollector>();
        var original = TelemetryFactory.SetMetricsForTesting(mockCollector.Object);
        try
        {
            var settings = CreateSettings(
                (ConfigurationKeys.CIVisibility.DynamicAtrEnabled, "true"),
                (ConfigurationKeys.CIVisibility.DynamicAtrBuckets, "3,1,1,1,1"),
                (ConfigurationKeys.CIVisibility.FlakyRetryEnabled, "true"));
            var flakyRetryFeature = CreateFlakyRetryFeature(settings, backendEnabled: false);

            TestOptimization.RecordDynamicAtrTelemetry(settings, flakyRetryFeature);

            mockCollector.Verify(
                c => c.RecordCountCIVisibilityDynamicAtrRetries(
                    It.IsAny<MetricTags.CIVisibilityDynamicAtrRetriesHasCustomBuckets>(),
                    It.IsAny<int>()),
                Times.Never);
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(original);
        }
    }

    private static ITestOptimizationFlakyRetryFeature CreateFlakyRetryFeature(TestOptimizationSettings settings, bool backendEnabled)
    {
        return TestOptimizationFlakyRetryFeature.Create(
            settings,
            new TestOptimizationClient.SettingsResponse(
                codeCoverage: false,
                testsSkipping: false,
                requireGit: false,
                impactedTestsEnabled: false,
                flakyTestRetries: backendEnabled,
                earlyFlakeDetection: new TestOptimizationClient.EarlyFlakeDetectionSettingsResponse(),
                knownTestsEnabled: false,
                testManagement: new TestOptimizationClient.TestManagementSettingsResponse(),
                dynamicInstrumentationEnabled: false),
            isRemoteSettingsResponse: true);
    }

    private static TestOptimizationSettings CreateSettings(params (string Key, string Value)[] values)
    {
        CoverageBackfillCapability.ResetCommandLineCacheForTests();
        return new TestOptimizationSettings(CreateConfigurationSource(values), NullConfigurationTelemetry.Instance);
    }
}
