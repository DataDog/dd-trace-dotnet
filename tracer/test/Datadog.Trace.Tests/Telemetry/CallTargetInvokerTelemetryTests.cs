// <copyright file="CallTargetInvokerTelemetryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.ContinuousProfiler;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class CallTargetInvokerTelemetryTests
    {
        // This test tests a lot at once because everything is heavily statically cached,
        // which makes tests brittle to order and concurrency etc
        [Fact]
        public void RecordsRelevantTelemetry()
        {
            var settings = new TracerSettings() { ServiceName = "DefaultService" };
            var telemetry = new TestTelemetryController();
            var tracer = new Tracer(
                settings,
                new Mock<IAgentWriter>().Object,
                new Mock<ITraceSampler>().Object,
                scopeManager: null,
                statsd: null,
                telemetry: telemetry);

            Tracer.UnsafeSetTracerInstance(tracer);
            telemetry.RunningInvocations.Should().BeEmpty();

            try
            {
                CallTargetInvoker.BeginMethod<HttpClientHandlerIntegration, HttpClientHandler>(new HttpClientHandler());
            }
            catch (Exception)
            {
                // this will throw but we don't actually care
            }

            telemetry.RunningInvocations.Should().ContainSingle();

            var runInvocation = telemetry.RunningInvocations[0];
            runInvocation.Should().Be(IntegrationId.HttpMessageHandler);

            try
            {
                CallTargetInvoker.BeginMethod<HttpClientHandlerIntegration, HttpClientHandler>(new HttpClientHandler());
            }
            catch (Exception)
            {
                // this will throw but we don't actually care
            }

            telemetry.RunningInvocations.Should().HaveCount(2);
            telemetry.RunningInvocations.Should().AllBeEquivalentTo(runInvocation);

            // Now track errors

            var untrackedException = new Exception("I'm not tracked");
            CallTargetInvoker.LogException<HttpClientHandlerIntegration, HttpClientHandler>(untrackedException);
            telemetry.ErrorInvocations.Should().BeEmpty();

            // Either of these should work, but we can't test both, as we only record the first exception for now
            Exception exception = null;
            try
            {
                DuckTypeException.Throw("A DuckTypeException occured");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // var exception = new CallTargetInvokerException(new Exception("A CallTargetInvokerException occurred"));

            CallTargetInvoker.LogException<HttpClientHandlerIntegration, HttpClientHandler>(exception);
            telemetry.ErrorInvocations.Should().ContainSingle();

            var invocation = telemetry.ErrorInvocations[0];
            invocation.Info.Should().Be(IntegrationId.HttpMessageHandler);
            invocation.Error.Should().Be(nameof(DuckTypeException));
        }

        internal class TestTelemetryController : ITelemetryController
        {
            public List<IntegrationId> RunningInvocations { get; } = new();

            public List<(IntegrationId Info, string Error)> ErrorInvocations { get; } = new();

            public void IntegrationRunning(IntegrationId info)
            {
                RunningInvocations.Add(info);
            }

            public void IntegrationGeneratedSpan(IntegrationId info)
            {
            }

            public void IntegrationDisabledDueToError(IntegrationId info, string error)
            {
                ErrorInvocations.Add((info, error));
            }

            public void RecordTracerSettings(ImmutableTracerSettings settings, string defaultServiceName)
            {
            }

            public void RecordProfilerSettings(Profiler profiler)
            {
            }

            public Task DisposeAsync()
            {
                return Task.CompletedTask;
            }

            public void Start()
            {
            }

            public void ProductChanged(TelemetryProductType product, bool enabled, ErrorData? error)
            {
            }

            public Task DumpTelemetry(string filePath) => Task.CompletedTask;

            public void RecordGitMetadata(GitMetadata gitMetadata)
            {
            }
        }
    }
}
