// <copyright file="AzureFunctionsDurableTriggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET8_0_OR_GREATER

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Azure;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
[Collection(AzureMessagingEmulatorTestsCollection.Name)]
[Trait("Category", "EndToEnd")]
[Trait("Category", "AzureFunctions")]
[Trait("RequiresDockerDependency", "true")]
[Trait("DockerGroup", "2")]
[Trait("Area", "AzureFunctions")]
[Trait("Category", "ArmUnsupported")]
public class AzureFunctionsDurableTriggerTests : AzureFunctionsTests
{
    private const int ExpectedDurableSpanCount = 8;
    private const string ExpectedFailureMessage = "Unable to greet World.";
    private const string ExpectedImmediateFailureMessage = "Unable to start orchestration.";
    private const string ManualActivitySpanName = "Manual inside DurableActivity";
    private const string LocalDurableTaskSchedulerConnectionString = "Endpoint=http://localhost:8080;TaskHub=default;Authentication=None";
    private const string AzuriteAccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly string[] ExpectedDurableResources =
    [
        "DurableOrchestration DurableWorkflow",
        "DurableActivity DurableActivity",
        "DurableEntity DurableCounter",
        ManualActivitySpanName,
        "DurableOrchestration FailingDurableWorkflow",
        "DurableActivity FailingDurableActivity",
        "DurableOrchestration ImmediatelyFailingDurableWorkflow",
    ];

    public AzureFunctionsDurableTriggerTests(ITestOutputHelper output)
        : base("AzureFunctions.V4Isolated.Durable", output)
    {
        SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
        SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        SetEnvironmentVariable("WEBSITE_SITE_NAME", nameof(AzureFunctionsDurableTriggerTests));
        SetEnvironmentVariable("AzureWebJobsStorage", GetAzuriteConnectionString());
        SetEnvironmentVariable("DURABLE_TASK_SCHEDULER_CONNECTION_STRING", GetDurableTaskSchedulerConnectionString());
        SetEnvironmentVariable("TASKHUB_NAME", "default");
        SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "false");
    }

    private static int ExpectedFuncKillExitCode
        => EnvironmentTools.IsWindows() ? -1 : 137;

    [SkippableFact]
    public async Task OrchestrationActivityEntity_SubmitsTrace()
    {
        Skip.If(EnvironmentHelper.IsAlpine(), "Azure Functions Core Tools are not installed in the Alpine integration test image.");

        var testId = Guid.NewGuid().ToString("N");
        SetEnvironmentVariable("AzureFunctionsWebHost__hostid", "afdurable" + testId.Substring(0, 23));

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        using (await RunAzureFunctionAndWaitForExit(
                   agent,
                   seedAsync: SeedViaHttpAsync,
                   expectedExitCode: ExpectedFuncKillExitCode))
        {
            var spans = await WaitForDurableSpansAsync(agent);

            spans.Should().HaveCount(ExpectedDurableSpanCount);
            var orchestrationSpan = spans.Should().ContainSingle(s => s.Resource == "DurableOrchestration DurableWorkflow").Subject;
            orchestrationSpan.Error.Should().Be(0);
            var activitySpan = spans.Should().ContainSingle(s => s.Resource == "DurableActivity DurableActivity").Subject;
            spans.Should().ContainSingle(s => HasTrigger(s, "DurableEntity"));
            var manualSpan = spans.Should().ContainSingle(s => s.Name == ManualActivitySpanName).Subject;
            manualSpan.TraceId.Should().Be(activitySpan.TraceId);
            manualSpan.ParentId.Should().Be(activitySpan.SpanId);

            spans.Where(s => s.Resource is "DurableOrchestration DurableWorkflow" or "DurableActivity DurableActivity")
                 .Should()
                 .OnlyContain(s => s.TraceId == orchestrationSpan.TraceId)
                 .And
                 .OnlyContain(
                      s => HasPositiveSamplingPriority(s),
                      "Azure's implicit rejection must not prevent local Datadog sampling");

            var failingActivitySpan = spans.Should().ContainSingle(s => s.Resource == "DurableActivity FailingDurableActivity").Subject;
            failingActivitySpan.Error.Should().Be(1);
            failingActivitySpan.Tags.Should().ContainKey(Tags.ErrorMsg).WhoseValue.Should().Contain(ExpectedFailureMessage);

            var failingOrchestrationSpans = spans.Where(s => s.Resource == "DurableOrchestration FailingDurableWorkflow").ToList();
            failingOrchestrationSpans.Should().HaveCount(2, "only the initial execution and the failed replay should be traced");
            failingOrchestrationSpans.Should().ContainSingle(s => s.Error == 0);
            var failedOrchestrationSpan = failingOrchestrationSpans.Should().ContainSingle(s => s.Error == 1).Subject;
            failedOrchestrationSpan.Tags.Should().ContainKey(Tags.ErrorMsg).WhoseValue.Should().Contain(ExpectedFailureMessage);

            spans.Where(s => s.Resource is "DurableOrchestration FailingDurableWorkflow" or "DurableActivity FailingDurableActivity")
                 .Should()
                 .OnlyContain(s => s.TraceId == failedOrchestrationSpan.TraceId)
                 .And
                 .OnlyContain(s => HasPositiveSamplingPriority(s));

            var immediatelyFailedOrchestrationSpan = spans.Should().ContainSingle(s => s.Resource == "DurableOrchestration ImmediatelyFailingDurableWorkflow").Subject;
            immediatelyFailedOrchestrationSpan.Error.Should().Be(1);
            immediatelyFailedOrchestrationSpan.Tags.Should().ContainKey(Tags.ErrorMsg).WhoseValue.Should().Contain(ExpectedImmediateFailureMessage);
            HasPositiveSamplingPriority(immediatelyFailedOrchestrationSpan).Should().BeTrue();

            await AssertIsolatedSpans(
                spans,
                $"{nameof(AzureFunctionsDurableTriggerTests)}.{nameof(OrchestrationActivityEntity_SubmitsTrace)}");
        }
    }

    private static bool HasTrigger(MockSpan span, string expectedTrigger)
        => span.Tags.TryGetValue("aas.function.trigger", out var trigger) && trigger == expectedTrigger;

    private static bool HasPositiveSamplingPriority(MockSpan span)
        => span.Metrics.TryGetValue(Metrics.SamplingPriority, out var samplingPriority) && samplingPriority > 0;

    private static IImmutableList<MockSpan> GetDurableSpans(IImmutableList<MockSpan> spans)
        => spans.Where(
                    s => ExpectedDurableResources.Contains(s.Resource)
                      && (s.Name == ManualActivitySpanName
                       || (s.Name == "azure_functions.invoke"
                        && s.Tags.TryGetValue("aas.function.trigger", out var trigger)
                        && trigger.StartsWith("Durable", StringComparison.Ordinal))))
                .ToImmutableList();

    private static async Task<IImmutableList<MockSpan>> WaitForDurableSpansAsync(MockTracerAgent agent)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        IImmutableList<MockSpan> spans;
        do
        {
            spans = GetDurableSpans(agent.Spans);
            if (spans.Count >= ExpectedDurableSpanCount)
            {
                return spans;
            }

            await Task.Delay(250);
        }
        while (DateTime.UtcNow < deadline);

        return spans;
    }

    private static async Task SeedViaHttpAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        using var response = await http.PostAsync("http://localhost:7071/api/seed/durable", content: null);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Durable workflows returned {(int)response.StatusCode}, expected {(int)HttpStatusCode.OK}: {responseBody}");
        }

        responseBody.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Should().Equal("Completed", "Failed", "Failed");
    }

    private static string GetDurableTaskSchedulerConnectionString()
        => Environment.GetEnvironmentVariable("DURABLE_TASK_SCHEDULER_CONNECTION_STRING") ?? LocalDurableTaskSchedulerConnectionString;

    private static string GetAzuriteConnectionString()
    {
        var host = GetDurableTaskSchedulerConnectionString().Contains("durabletask-scheduler", StringComparison.OrdinalIgnoreCase)
                       ? "azurite"
                       : "127.0.0.1";
        return $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={AzuriteAccountKey};BlobEndpoint=http://{host}:10000/devstoreaccount1;QueueEndpoint=http://{host}:10001/devstoreaccount1;TableEndpoint=http://{host}:10002/devstoreaccount1;";
    }
}

#endif
