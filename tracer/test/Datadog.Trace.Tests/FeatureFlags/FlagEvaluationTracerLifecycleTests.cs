// <copyright file="FlagEvaluationTracerLifecycleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.FeatureFlags;

[Collection(nameof(WebRequestCollection))]
public class FlagEvaluationTracerLifecycleTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task PublicFlushWaitsForBothTracesAndFlagEvaluations()
    {
        using var agent = MockTracerAgent.Create(_output);
        var received = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Enqueue(args.Value);
        var traceWriter = new Mock<IAgentWriter>();
        traceWriter.Setup(writer => writer.FlushTracesAsync()).Returns(Task.CompletedTask);
        traceWriter.Setup(writer => writer.FlushAndCloseAsync()).Returns(Task.CompletedTask);
        await using var tracer = TracerHelper.Create(Settings(agent.Port), agentWriter: traceWriter.Object);
        var module = tracer.TracerManager.FeatureFlags!;
        module.Activate();
        module.EvaluationWriter!.TryEnqueue(Observation()).Should().BeTrue();

        await tracer.FlushAsync();

        traceWriter.Verify(writer => writer.FlushTracesAsync(), Times.Once);
        received.Should().ContainSingle("public flush must not leave events waiting for the ten-second timer");
    }

    [Fact]
    public async Task ShutdownWaitsForBoundedFlagFlushBeforeFinalTelemetry()
    {
        using var agent = MockTracerAgent.Create(_output);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.EventPlatformProxyPayloadReceived += (_, _) =>
        {
            entered.TrySetResult(true);
            release.Task.GetAwaiter().GetResult();
        };
        var traceWriter = new Mock<IAgentWriter>();
        traceWriter.Setup(writer => writer.FlushAndCloseAsync()).Returns(Task.CompletedTask);
        var telemetry = new Mock<ITelemetryController>();
        telemetry.Setup(controller => controller.DisposeAsync()).Returns(Task.CompletedTask);
        var tracer = TracerHelper.Create(Settings(agent.Port), agentWriter: traceWriter.Object, telemetryController: telemetry.Object);
        try
        {
            var module = tracer.TracerManager.FeatureFlags!;
            module.Activate();
            module.EvaluationWriter!.TryEnqueue(Observation()).Should().BeTrue();
            var shutdown = tracer.DisposeAsync().AsTask();
            (await Task.WhenAny(entered.Task, Task.Delay(TimeSpan.FromSeconds(2)))).Should().BeSameAs(entered.Task);
            telemetry.Verify(controller => controller.DisposeAsync(), Times.Never, "the existing final telemetry send should include metrics available from the bounded drain");
            shutdown.IsCompleted.Should().BeFalse();
            release.TrySetResult(true);
            await shutdown;
            telemetry.Verify(controller => controller.DisposeAsync(), Times.Once);
        }
        finally
        {
            release.TrySetResult(true);
            await tracer.DisposeAsync();
        }
    }

    private static TracerSettings Settings(int port) => new(new NameValueConfigurationSource(new NameValueCollection
    {
        [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = "remote_config",
        [ConfigurationKeys.AgentUri] = $"http://127.0.0.1:{port}",
        [ConfigurationKeys.ServiceName] = "lifecycle-test",
    }));

    private static FlagEvalEvent Observation() => new("flag", "on", null, "private-subject", 1790000000000, null);
}
