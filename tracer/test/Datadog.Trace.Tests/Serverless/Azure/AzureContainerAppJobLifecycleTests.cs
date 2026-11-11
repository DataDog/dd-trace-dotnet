// <copyright file="AzureContainerAppJobLifecycleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Serverless;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Serverless;

[Collection(nameof(EnvironmentVariablesTestCollection))]
[EnvironmentRestorer(
    PlatformKeys.AzureAppService.ContainerAppJobName,
    PlatformKeys.AzureAppService.ContainerAppJobExecutionName)]
public class AzureContainerAppJobLifecycleTests
{
    [Fact]
    public async Task TryStartCreatesProcessRootAndParentsAutomaticWork()
    {
        const string jobName = "hourly-payment-job";
        const string executionName = "hourly-payment-job-abc123";
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppJobName, jobName);
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppJobExecutionName, executionName);
        var tracer = CreateTracer(traceEnabled: true);

        ServerlessInitialization.Initialize(tracer, new AzureInfo());
        var lifecycle = tracer.TracerManager.ActiveProcessLifetimeRootSpan.Should().BeOfType<AzureContainerAppJobLifecycle>().Subject;

        lifecycle.Span.OperationName.Should().Be(AzureContainerAppJobLifecycle.OperationName);
        lifecycle.Span.ResourceName.Should().Be(jobName);
        lifecycle.Span.ServiceName.Should().Be("job-service");
        lifecycle.Span.Type.Should().Be(SpanTypes.Serverless);
        lifecycle.Span.GetTag(Tags.SpanKind).Should().Be(SpanKinds.Internal);
        lifecycle.Span.GetTag(Tags.AzureContainerAppJobName).Should().Be(jobName);
        lifecycle.Span.GetTag(Tags.AzureContainerAppJobExecutionName).Should().Be(executionName);

        using (var child = tracer.StartActiveInternal("outbound-http"))
        {
            child.Span.Context.ParentId.Should().Be(lifecycle.Span.SpanId);
        }

        Task<ulong?> backgroundWork;
        using (ExecutionContext.SuppressFlow())
        {
            backgroundWork = Task.Run(() =>
            {
                using var child = tracer.StartActiveInternal("background-work");
                return child.Span.Context.ParentId;
            });
        }

        var fallbackParentId = await backgroundWork;
        fallbackParentId.Should().Be(lifecycle.Span.SpanId);

        var explicitParent = new SpanContext(123, 456, SamplingPriority.UserKeep);
        using (var explicitlyParented = tracer.StartActiveInternal("explicit-parent", parent: explicitParent))
        {
            explicitlyParented.Span.Context.ParentId.Should().Be(explicitParent.SpanId);
        }

        lifecycle.Complete(exception: null);

        lifecycle.Span.IsFinished.Should().BeTrue();
        lifecycle.SpanContext.Should().BeNull();
        tracer.TracerManager.ProcessLifetimeRootSpanContext.Should().BeNull();
        tracer.InternalActiveScope.Should().BeNull();
    }

    [Fact]
    public void CompleteRecordsUnhandledExceptionAndIsIdempotent()
    {
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppJobName, "failed-job");
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppJobExecutionName, "failed-job-abc123");
        var tracer = CreateTracer(traceEnabled: true);
        var lifecycle = AzureContainerAppJobLifecycle.TryStart(tracer, new AzureInfo())!;
        var exception = new InvalidOperationException("boom");

        lifecycle.Complete(exception);
        lifecycle.Complete(exception);

        lifecycle.Span.IsFinished.Should().BeTrue();
        lifecycle.Span.Error.Should().BeTrue();
        lifecycle.Span.GetTag(Tags.ErrorType).Should().Contain(nameof(InvalidOperationException));
        lifecycle.Span.GetTag(Tags.ErrorMsg).Should().Be("boom");
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("job", null, true)]
    [InlineData(null, "execution", true)]
    [InlineData("job", "execution", false)]
    public void TryStartRequiresCompleteJobIdentityAndTracing(string jobName, string executionName, bool traceEnabled)
    {
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppJobName, jobName);
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppJobExecutionName, executionName);
        var tracer = CreateTracer(traceEnabled);

        AzureContainerAppJobLifecycle.TryStart(tracer, new AzureInfo()).Should().BeNull();
    }

    [Fact]
    public async Task ShutdownWritesProcessRootBeforeClosingWriter()
    {
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppJobName, "ordered-job");
        Environment.SetEnvironmentVariable(PlatformKeys.AzureAppService.ContainerAppJobExecutionName, "ordered-job-abc123");
        var writer = new RecordingAgentWriter();
        var tracer = CreateTracer(traceEnabled: true, writer);

        ServerlessInitialization.Initialize(tracer, new AzureInfo());
        await tracer.TracerManager.ShutdownAsync();

        writer.Events.Should().Equal("write", "flush-and-close");
        tracer.TracerManager.ProcessLifetimeRootSpanContext.Should().BeNull();
    }

    [Fact]
    public async Task InitializationRunsOnceAndCompletionFailureDoesNotSkipWriterShutdown()
    {
        var writer = new RecordingAgentWriter();
        var tracer = CreateTracer(traceEnabled: true, writer);
        var processRoot = new ThrowingProcessLifetimeRootSpan();
        var factoryCalls = 0;

        tracer.TracerManager.InitializeProcessLifetimeRootSpan(
            () =>
            {
                factoryCalls++;
                return processRoot;
            });
        tracer.TracerManager.InitializeProcessLifetimeRootSpan(
            () =>
            {
                factoryCalls++;
                return processRoot;
            });

        await tracer.TracerManager.ShutdownAsync();

        factoryCalls.Should().Be(1);
        processRoot.CompletionCalls.Should().Be(1);
        writer.Events.Should().Equal("flush-and-close");
    }

    private static Tracer CreateTracer(bool traceEnabled, IAgentWriter writer = null)
    {
        var source = new NameValueConfigurationSource(
            new NameValueCollection
            {
                { ConfigurationKeys.ServiceName, "job-service" },
                { ConfigurationKeys.TraceEnabled, traceEnabled.ToString() },
                { ConfigurationKeys.AgentFeaturePollingEnabled, bool.FalseString },
            });
        var settings = new TracerSettings(source);

        return new Tracer(
            settings,
            writer ?? Mock.Of<IAgentWriter>(),
            Mock.Of<ITraceSampler>(),
            scopeManager: null,
            statsd: null);
    }

    private sealed class RecordingAgentWriter : IAgentWriter
    {
        public List<string> Events { get; } = new();

        public void WriteTrace(in SpanCollection trace) => Events.Add("write");

        public Task<bool> Ping() => Task.FromResult(true);

        public Task FlushTracesAsync() => Task.CompletedTask;

        public Task FlushAndCloseAsync()
        {
            Events.Add("flush-and-close");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingProcessLifetimeRootSpan : IProcessLifetimeRootSpan
    {
        public int CompletionCalls { get; private set; }

        public SpanContext SpanContext => null;

        public void Complete(Exception exception)
        {
            CompletionCalls++;
            throw new InvalidOperationException("expected test exception");
        }
    }
}
