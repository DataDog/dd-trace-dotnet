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
    private const int ExpectedDurableSpanCount = 3;
    private const int ExpectedFailingDurableSpanCount = 3;
    private const string ExpectedFailureMessage = "Unable to greet World.";
    private const string LocalDurableTaskSchedulerConnectionString = "Endpoint=http://localhost:8080;TaskHub=default;Authentication=None";
    private const string AzuriteAccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly string[] ExpectedDurableResources =
    [
        "DurableOrchestration DurableWorkflow",
        "DurableActivity DurableActivity",
        "DurableEntity DurableCounter",
    ];

    private static readonly string[] ExpectedFailingDurableResources =
    [
        "DurableOrchestration FailingDurableWorkflow",
        "DurableActivity FailingDurableActivity",
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
        SetEnvironmentVariable("DD_TRACE_SAMPLE_RATE", "1");
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
                   seedAsync: () => SeedViaHttpAsync("seed/durable", HttpStatusCode.Accepted),
                   expectedExitCode: ExpectedFuncKillExitCode))
        {
            var spans = await WaitForDurableSpansAsync(agent);

            spans.Should().HaveCount(ExpectedDurableSpanCount);
            spans.Should().ContainSingle(s => HasTrigger(s, "DurableOrchestration")).Which.Error.Should().Be(0);
            spans.Should().ContainSingle(s => HasTrigger(s, "DurableActivity"));
            spans.Should().ContainSingle(s => HasTrigger(s, "DurableEntity"));

            var workflowTraceId = spans.First(s => HasTrigger(s, "DurableOrchestration")).TraceId;
            spans.Where(s => HasTrigger(s, "DurableOrchestration") || HasTrigger(s, "DurableActivity"))
                 .Should()
                 .OnlyContain(s => s.TraceId == workflowTraceId)
                 .And
                 .OnlyContain(
                      s => HasPositiveSamplingPriority(s),
                      "Azure's implicit rejection must not prevent local Datadog sampling");

            await AssertIsolatedSpans(
                spans,
                $"{nameof(AzureFunctionsDurableTriggerTests)}.{nameof(OrchestrationActivityEntity_SubmitsTrace)}");
        }
    }

    [SkippableFact]
    public async Task OrchestrationActivity_ErrorPropagates()
    {
        Skip.If(EnvironmentHelper.IsAlpine(), "Azure Functions Core Tools are not installed in the Alpine integration test image.");

        var testId = Guid.NewGuid().ToString("N");
        SetEnvironmentVariable("AzureFunctionsWebHost__hostid", "afdurable" + testId.Substring(0, 23));

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        using (await RunAzureFunctionAndWaitForExit(
                   agent,
                   seedAsync: () => SeedViaHttpAsync("seed/durable/error", HttpStatusCode.Accepted),
                   expectedExitCode: ExpectedFuncKillExitCode))
        {
            var spans = await WaitForDurableSpansAsync(agent, ExpectedFailingDurableResources, ExpectedFailingDurableSpanCount);

            spans.Should().HaveCount(ExpectedFailingDurableSpanCount);
            var activitySpan = spans.Should().ContainSingle(s => HasTrigger(s, "DurableActivity")).Subject;
            activitySpan.Error.Should().Be(1);
            activitySpan.Tags.Should().ContainKey(Tags.ErrorMsg).WhoseValue.Should().Contain(ExpectedFailureMessage);

            var orchestrationSpans = spans.Where(s => HasTrigger(s, "DurableOrchestration")).ToList();
            orchestrationSpans.Should().HaveCount(2, "only the initial execution and the failed replay should be traced");
            orchestrationSpans.Should().ContainSingle(s => s.Error == 0);
            var failedOrchestrationSpan = orchestrationSpans.Should().ContainSingle(s => s.Error == 1).Subject;
            failedOrchestrationSpan.Tags.Should().ContainKey(Tags.ErrorMsg).WhoseValue.Should().Contain(ExpectedFailureMessage);

            var workflowTraceId = spans.First(s => HasTrigger(s, "DurableOrchestration")).TraceId;
            spans.Should()
                 .OnlyContain(s => s.TraceId == workflowTraceId)
                 .And
                 .OnlyContain(s => HasPositiveSamplingPriority(s));

            await AssertIsolatedSpans(
                spans,
                $"{nameof(AzureFunctionsDurableTriggerTests)}.{nameof(OrchestrationActivity_ErrorPropagates)}");
        }
    }

    private static bool HasTrigger(MockSpan span, string expectedTrigger)
        => span.Tags.TryGetValue("aas.function.trigger", out var trigger) && trigger == expectedTrigger;

    private static bool HasPositiveSamplingPriority(MockSpan span)
        => span.Metrics.TryGetValue(Metrics.SamplingPriority, out var samplingPriority) && samplingPriority > 0;

    private static IImmutableList<MockSpan> GetDurableSpans(IImmutableList<MockSpan> spans, string[] expectedResources)
        => spans.Where(
                    s => s.Name == "azure_functions.invoke"
                      && expectedResources.Contains(s.Resource)
                      && s.Tags.TryGetValue("aas.function.trigger", out var trigger)
                      && trigger.StartsWith("Durable", StringComparison.Ordinal))
                .ToImmutableList();

    private static Task<IImmutableList<MockSpan>> WaitForDurableSpansAsync(MockTracerAgent agent)
        => WaitForDurableSpansAsync(agent, ExpectedDurableResources, ExpectedDurableSpanCount);

    private static async Task<IImmutableList<MockSpan>> WaitForDurableSpansAsync(MockTracerAgent agent, string[] expectedResources, int expectedSpanCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        IImmutableList<MockSpan> spans;
        do
        {
            spans = GetDurableSpans(agent.Spans, expectedResources);
            if (spans.Count >= expectedSpanCount)
            {
                return spans;
            }

            await Task.Delay(250);
        }
        while (DateTime.UtcNow < deadline);

        return spans;
    }

    private static async Task SeedViaHttpAsync(string route, HttpStatusCode expectedStatusCode)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        using var response = await http.PostAsync($"http://localhost:7071/api/{route}", content: null);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expectedStatusCode)
        {
            throw new InvalidOperationException($"Durable workflow returned {(int)response.StatusCode}, expected {(int)expectedStatusCode}: {responseBody}");
        }
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
