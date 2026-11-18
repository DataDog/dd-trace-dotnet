// <copyright file="FlagEvaluationBridgeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

// Some tests swap the global metrics collector, which other test classes also use.
[Collection(nameof(TracerInstanceTestCollection))]
public class FlagEvaluationBridgeTests
{
    private delegate CallTargetState EnqueueCallback(object? target, ref string? flag, ref string? variant, ref string? allocation, ref string? subject, ref long time, ref string? error, ref IReadOnlyDictionary<string, object?>? attributes, ref int flags);

    [Theory]
    [InlineData(typeof(OpenFeatureSdkCanEnqueueEVPIntegration))]
    [InlineData(typeof(OpenFeatureSdkEnqueueEVPIntegration))]
    [InlineData(typeof(OpenFeatureSdkCanEnqueueEVPWithFeaturesIntegration))]
    [InlineData(typeof(OpenFeatureSdkEnqueueEVPWithFeaturesIntegration))]
    [InlineData(typeof(OpenFeatureSdkCanDeferExposuresIntegration))]
    [InlineData(typeof(OpenFeatureSdkEvaluateDeferredIntegration))]
    [InlineData(typeof(OpenFeatureSdkCommitExposureIntegration))]
    [InlineData(typeof(OpenFeatureSdkRecordEVPHookErrorIntegration))]
    [InlineData(typeof(OpenFeatureSdkIsEvaluationEventsEnabledIntegration))]
    public void GeneratedDefinitionsRegisterCallbacksAsOpenFeature(Type integration)
        => InstrumentationDefinitions.GetIntegrationId(integration.FullName!, typeof(object)).Should().Be(IntegrationId.OpenFeature);

    [Fact]
    public void EnqueueCallbackBindsThroughCallTargetFastPath()
    {
        var integration = typeof(OpenFeatureSdkEnqueueEVPIntegration);
        Type[] arguments = [typeof(string), typeof(string), typeof(string), typeof(string), typeof(long), typeof(string), typeof(IReadOnlyDictionary<string, object?>), typeof(int)];
        var generated = IntegrationMapper.CreateBeginMethodDelegate(integration, typeof(object), arguments.Select(type => type.MakeByRefType()).ToArray())!;
        var invoke = (EnqueueCallback)generated.CreateDelegate(typeof(EnqueueCallback));
        string? flag = "flag", variant = "on", subject = "subject";
        string? allocation = null, error = null;
        long time = 1790000000000L;
        IReadOnlyDictionary<string, object?>? attributes = null;
        var flags = 0;
        Action call = () => invoke(null, ref flag, ref variant, ref allocation, ref subject, ref time, ref error, ref attributes, ref flags);
        call.Should().NotThrow();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefensiveSnapshotFailurePreservesEvaluationAndProtectedModeNeverReadsAttributes(bool consent)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var bodies = new ConcurrentQueue<string>();
        var writer = Writer(bodies, collector);
        var attributes = new Mock<IReadOnlyDictionary<string, object?>>(MockBehavior.Strict);
        attributes.SetupGet(value => value.Count).Throws(new InvalidOperationException("private-snapshot-error"));
        try
        {
            OpenFeatureSdkEnqueueEVPIntegration.Enqueue(writer, "flag", "on", "allocation", "private-subject", 1790000000000, "private-error", attributes.Object, consent ? FlagEvaluationBridge.ObserveFullEvaluationData : 0);
            await writer.FlushAsync();
            bodies.Should().ContainSingle();
            var body = bodies.Single();
            body.Should().NotContain("private-error").And.NotContain("private-snapshot-error");
            var row = JObject.Parse(body)["flagEvaluations"]![0]!;
            row["evaluation_count"]!.Value<int>().Should().Be(1);
            row["first_evaluation"]!.Value<long>().Should().Be(1790000000000);
            row["allocation"]!["key"]!.Value<string>().Should().Be("allocation");
            row["context"].Should().BeNull();
            row["targeting_key"]!.Value<string>().Should().Be(consent ? "private-subject" : FlagEvaluationPrivacy.TargetingKeyForOutput("private-subject", false));
            attributes.VerifyGet(value => value.Count, consent ? Times.Once() : Times.Never());
            collector.AggregateMetrics();
            var metrics = collector.GetMetrics().Metrics;
            if (consent)
            {
                metrics.Should().ContainSingle();
                var metric = metrics!.Single();
                metric.Metric.Should().Be("flagevaluation.context.truncated");
                metric.Tags.Should().Equal("reason:snapshot_error");
                metric.Points.Sum(point => point.Value).Should().Be(1);
            }
            else
            {
                metrics.Should().BeNullOrEmpty();
            }
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Fact]
    public async Task BridgeDetachesConsentedAttributesAndForwardsProviderOmissionReasons()
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var bodies = new ConcurrentQueue<string>();
        var writer = Writer(bodies, collector);
        var attributes = new Dictionary<string, object?> { ["country"] = "before", ["oversized"] = new string('x', 257) };
        try
        {
            OpenFeatureSdkEnqueueEVPIntegration.Enqueue(writer, "flag", "on", null, "subject", 1790000000000, null, attributes, FlagEvaluationBridge.ObserveFullEvaluationData | (int)ContextOmissionReason.MaxValueLength);
            attributes["country"] = "after";
            await writer.FlushAsync();
            bodies.Should().ContainSingle();
            JObject.Parse(bodies.Single())["flagEvaluations"]![0]!["context"]!["evaluation"]!["country"]!.Value<string>().Should().Be("before");
            collector.AggregateMetrics();
            var metric = collector.GetMetrics().Metrics!.Should().ContainSingle().Subject;
            metric.Metric.Should().Be("flagevaluation.context.truncated");
            metric.Tags.Should().Equal("reason:max_value_length");
            metric.Points.Sum(point => point.Value).Should().Be(1, "the provider and defensive normalization reported the same reason");
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Fact]
    public void MissingWriterNeverInspectsContext()
    {
        var attributes = new Mock<IReadOnlyDictionary<string, object?>>(MockBehavior.Strict);
        OpenFeatureSdkEnqueueEVPIntegration.Enqueue(null, "flag", null, null, "subject", 1790000000000, null, attributes.Object, FlagEvaluationBridge.ObserveFullEvaluationData);
        attributes.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0x1ff, false)]
    [InlineData(0x40000000, true)]
    [InlineData(0x400001ff, true)]
    public async Task BridgeConsentBitIsIndependentOfOmissionReasons(int flags, bool consent)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var bodies = new ConcurrentQueue<string>();
        var writer = Writer(bodies, collector);
        try
        {
            OpenFeatureSdkEnqueueEVPIntegration.Enqueue(writer, "flag", "on", null, "private-subject", 1790000000000, null, new Dictionary<string, object?> { ["country"] = "US" }, flags);
            await writer.FlushAsync();
            var row = JObject.Parse(bodies.Should().ContainSingle().Subject)["flagEvaluations"]![0]!;
            row["targeting_key"]!.Value<string>().Should().Be(consent ? "private-subject" : FlagEvaluationPrivacy.TargetingKeyForOutput("private-subject", false));
            if (consent)
            {
                row["context"]!["evaluation"]!["country"]!.Value<string>().Should().Be("US");
            }
            else
            {
                row["context"].Should().BeNull();
            }
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Fact]
    public void EnqueueWithFeaturesCallbackBindsThroughCallTargetSlowPath()
    {
        var generated = IntegrationMapper.CreateSlowBeginMethodDelegate(typeof(OpenFeatureSdkEnqueueEVPWithFeaturesIntegration), typeof(object))!;
        var invoke = (BeginMethodSlowHandler<OpenFeatureSdkEnqueueEVPWithFeaturesIntegration, object>.InvokeDelegate)generated.CreateDelegate(typeof(BeginMethodSlowHandler<OpenFeatureSdkEnqueueEVPWithFeaturesIntegration, object>.InvokeDelegate));
        object[] arguments = ["flag", "on", null!, "subject", 1790000000000L, null!, null!, new Dictionary<string, object?> { ["holdout.key"] = "h1" }, 0];
        Action call = () => invoke(null, arguments);
        call.Should().NotThrow();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BridgeForwardsEvaluationFeaturesWhateverTheConsent(bool consent)
    {
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var bodies = new ConcurrentQueue<string>();
        var writer = Writer(bodies, collector);
        try
        {
            var features = new Dictionary<string, object?> { ["holdout.key"] = "h1", ["holdout.should_include_in_holdout_analysis"] = true };
            OpenFeatureSdkEnqueueEVPIntegration.Enqueue(writer, "flag", "on", null, "subject", 1790000000000, null, null, consent ? FlagEvaluationBridge.ObserveFullEvaluationData : 0, features);
            await writer.FlushAsync();
            var row = JObject.Parse(bodies.Should().ContainSingle().Subject)["flagEvaluations"]![0]!;
            row["features"]!["holdout.key"]!.Value<string>().Should().Be("h1");
            row["features"]!["holdout.should_include_in_holdout_analysis"]!.Value<bool>().Should().BeTrue();
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Fact]
    public async Task EnqueueWithFeaturesKeepsAttributesAndFeaturesApart()
    {
        // Attributes without consent are never sent, so a swap with the features would leak them.
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var bodies = new ConcurrentQueue<string>();
        var writer = Writer(bodies, collector);
        try
        {
            IReadOnlyDictionary<string, object?> attrs = new Dictionary<string, object?> { ["email"] = "attr-canary@example.test" };
            IReadOnlyDictionary<string, object?> features = new Dictionary<string, object?> { ["holdout.key"] = "h1" };

            OpenFeatureSdkEnqueueEVPWithFeaturesIntegration.Enqueue(writer, "flag", "on", "allocation", "subject", 1790000000000L, (string?)null, attrs, features, 0);
            await writer.FlushAsync();

            var body = bodies.Should().ContainSingle().Subject;
            body.Should().NotContain("attr-canary");
            JObject.Parse(body)["flagEvaluations"]![0]!["features"]!["holdout.key"]!.Value<string>().Should().Be("h1");
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
            await collector.DisposeAsync();
        }
    }

    [Fact]
    public void EnqueueWithFeaturesSlowPathCastsEveryArgumentWithoutAHookError()
    {
        // OnMethodBegin turns any exception into a hook error, so a wrong cast is visible only there.
        // Start the tracer first: its startup replaces the global metrics collector.
        _ = TracerManager.Instance;
        var metrics = new Mock<IMetricsTelemetryCollector>();
        var previous = TelemetryFactory.SetMetricsForTesting(metrics.Object);
        try
        {
            var generated = IntegrationMapper.CreateSlowBeginMethodDelegate(typeof(OpenFeatureSdkEnqueueEVPWithFeaturesIntegration), typeof(object))!;
            var invoke = (BeginMethodSlowHandler<OpenFeatureSdkEnqueueEVPWithFeaturesIntegration, object>.InvokeDelegate)generated.CreateDelegate(typeof(BeginMethodSlowHandler<OpenFeatureSdkEnqueueEVPWithFeaturesIntegration, object>.InvokeDelegate));
            object[] arguments = ["flag", "on", "allocation", "subject", 1790000000000L, "GENERAL", new Dictionary<string, object?> { ["country"] = "US" }, new Dictionary<string, object?> { ["holdout.key"] = "h1" }, FlagEvaluationBridge.ObserveFullEvaluationData];

            invoke(null, arguments);

            metrics.Verify(m => m.RecordCountFlagEvaluationHookErrors(It.IsAny<int>()), Times.Never());
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(previous);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CanEnqueueWithFeaturesIsTrueUnlessTheStubThrew(bool threw, bool expected)
    {
        var state = CallTargetState.GetDefault();
        OpenFeatureSdkCanEnqueueEVPWithFeaturesIntegration.OnMethodEnd<object>(false, threw ? new InvalidOperationException() : null, in state).GetReturnValue().Should().Be(expected);
    }

    [Fact]
    public void CanEnqueueWithFeaturesIsFalseOnceTheDeliveryIntegrationIsDisabled()
    {
        var state = CallTargetState.GetDefault();
        OpenFeatureSdkCanEnqueueEVPWithFeaturesIntegration.OnMethodEnd<DisabledDeliveryTarget>(false, null, in state).GetReturnValue().Should().BeTrue();

        IntegrationOptions<OpenFeatureSdkEnqueueEVPWithFeaturesIntegration, DisabledDeliveryTarget>.DisableIntegration();

        OpenFeatureSdkCanEnqueueEVPWithFeaturesIntegration.OnMethodEnd<DisabledDeliveryTarget>(false, null, in state).GetReturnValue().Should().BeFalse("evaluations must fall back to EnqueueEVP");
    }

    [Fact]
    public void UnexpectedHookErrorIsBestEffortEvenWhenTheMetricSinkThrows()
    {
        var collector = new Mock<IMetricsTelemetryCollector>(MockBehavior.Strict);
        Action record = () => OpenFeatureSdkRecordEVPHookErrorIntegration.RecordError(collector.Object);
        record.Should().NotThrow();
        collector.Verify(metrics => metrics.RecordCountFlagEvaluationHookErrors(1), Times.Once);
    }

    private static FlagEvaluationWriter Writer(ConcurrentQueue<string> bodies, IMetricsTelemetryCollector metrics) => new(
        bytes =>
        {
            using var compressed = new MemoryStream(bytes.Array!, bytes.Offset, bytes.Count);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            bodies.Enqueue(reader.ReadToEnd());
            return Task.CompletedTask;
        },
        new Dictionary<string, string> { ["service"] = "bridge-test" },
        metrics: metrics);

    // A target type of its own, because disabling an integration is static state for that type.
    private sealed class DisabledDeliveryTarget
    {
    }
}
