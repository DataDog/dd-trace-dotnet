// <copyright file="AzureFunctionsMessagingTriggerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Datadog.Trace.ClrProfiler.IntegrationTests.Azure;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using VerifyTests;
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
public class AzureFunctionsMessagingTriggerTests : AzureFunctionsTests
{
    private const string ServiceBusQueueName = "samples-azureservicebus-queue";
    private const string EventHubName = "samples-eventhubs-hub";
    private const string EventHubConsumerGroup = "cg1";
    private const string TestIdEnvironmentVariable = "DD_AZURE_FUNCTIONS_MESSAGING_TEST_ID";
    private const string TestModeEnvironmentVariable = "DD_AZURE_FUNCTIONS_MESSAGING_TEST_MODE";
    private const string LocalServiceBusConnectionString = "Endpoint=sb://localhost:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
    private readonly AzureEventHubsFixture _eventHubsFixture;

    public AzureFunctionsMessagingTriggerTests(ITestOutputHelper output, AzureEventHubsFixture eventHubsFixture)
        : base("AzureFunctions.V4Isolated.Messaging", output)
    {
        _eventHubsFixture = eventHubsFixture;
        ConfigureContainers(eventHubsFixture);
        SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
        SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "~4");
        SetEnvironmentVariable("WEBSITE_SITE_NAME", nameof(AzureFunctionsMessagingTriggerTests));
        SetEnvironmentVariable("ASB_CONNECTION_STRING", GetServiceBusConnectionString());
    }

    private static int ExpectedFuncKillExitCode
        => EnvironmentTools.IsWindows() ? -1 : 137;

    [SkippableFact]
    public async Task ServiceBusTrigger_SubmitsTrace()
    {
        Skip.If(EnvironmentHelper.IsAlpine(), "Azure Functions Core Tools are not installed in the Alpine integration test image.");

        var testId = CreateTestId();
        SetEnvironmentVariable(TestModeEnvironmentVariable, "ServiceBus");
        SetEnvironmentVariable(TestIdEnvironmentVariable, testId);
        SetEnvironmentVariable("AzureFunctionsWebHost__hostid", CreateHostId(testId));
        SetEnvironmentVariable("AzureWebJobs.ServiceBusTrigger.Disabled", "false");
        SetEnvironmentVariable("AzureWebJobs.EventHubTrigger.Disabled", "true");

        await using (var client = new ServiceBusClient(GetServiceBusConnectionString()))
        await using (var receiver = client.CreateReceiver(ServiceBusQueueName))
        {
            await PurgeQueue(receiver);
        }

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        using (await RunAzureFunctionAndWaitForExit(
                   agent,
                   seedAsync: () => SeedViaHttpAsync("seed/servicebus"),
                   expectedExitCode: ExpectedFuncKillExitCode))
        {
            // 7 spans total: 1 health-check ping + 6 meaningful spans
            var allSpans = await agent.WaitForSpansAsync(7, timeoutInMilliseconds: 30000, returnAllOperations: true);
            // Filter out the health-check ping used to detect host readiness
            var spans = allSpans.Where(s => s.Resource != "GET /admin/host/ping").ToImmutableList();
            var settings = GetMessagingTriggerSettings();
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName($"{nameof(AzureFunctionsMessagingTriggerTests)}.{nameof(ServiceBusTrigger_SubmitsTrace)}")
                              .DisableRequireUniquePrefix();
        }
    }

    [SkippableFact]
    public async Task EventHubTrigger_SubmitsTrace()
    {
        Skip.If(EnvironmentHelper.IsAlpine(), "Azure Functions Core Tools are not installed in the Alpine integration test image.");

        var testId = CreateTestId();
        SetEnvironmentVariable(TestModeEnvironmentVariable, "EventHub");
        SetEnvironmentVariable(TestIdEnvironmentVariable, testId);
        SetEnvironmentVariable("AzureFunctionsWebHost__hostid", CreateHostId(testId));
        SetEnvironmentVariable("AzureWebJobs.ServiceBusTrigger.Disabled", "true");
        SetEnvironmentVariable("AzureWebJobs.EventHubTrigger.Disabled", "false");

        // Seed happens inside the running app (HTTP trigger), so the event is always enqueued
        // after the app starts. Start reading from now to avoid stale events from prior runs.
        var seedTime = DateTimeOffset.UtcNow;
        SetEnvironmentVariable("AzureFunctionsJobHost__extensions__eventHubs__initialOffsetOptions__type", "fromEnqueuedTime");
        SetEnvironmentVariable("AzureFunctionsJobHost__extensions__eventHubs__initialOffsetOptions__enqueuedTimeUtc", seedTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

        await ResetEventHubCheckpointStore();

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);
        using (await RunAzureFunctionAndWaitForExit(
                   agent,
                   seedAsync: () => SeedViaHttpAsync("seed/eventhub"),
                   expectedExitCode: ExpectedFuncKillExitCode))
        {
            // Wait for at least 7 spans (1 health-check ping + 6 meaningful).
            var allSpans = await agent.WaitForSpansAsync(7, timeoutInMilliseconds: 30000, returnAllOperations: true);
            // Keep only the two relevant traces: the seeder trace and the trigger trace.
            // The trigger trace is identified by the manual span, which is only created for
            // the expected test event.
            var filteredSpans = allSpans.Where(s => s.Resource != "GET /admin/host/ping").ToImmutableList();
            var manualSpan = filteredSpans.FirstOrDefault(s => s.Name == "Manual inside EventHubTrigger");
            var sendSpan = filteredSpans.FirstOrDefault(s => s.Name == "azure_eventhubs.send");
            var spans = filteredSpans
                        .Where(s => s.TraceId == manualSpan?.TraceId || s.TraceId == sendSpan?.TraceId)
                        .ToImmutableList();
            var settings = GetMessagingTriggerSettings();
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName($"{nameof(AzureFunctionsMessagingTriggerTests)}.{nameof(EventHubTrigger_SubmitsTrace)}")
                              .DisableRequireUniquePrefix();
        }
    }

    private static async Task SeedViaHttpAsync(string route)
    {
        // Retry on 404: the host may accept connections before all function routes are registered.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var url = $"http://localhost:7071/api/{route}";
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.PostAsync(url, content: null);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return;
                }

                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                // Host not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Function host did not accept route '{route}' within the timeout.");
    }

    private static async Task PurgeQueue(ServiceBusReceiver receiver)
    {
        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(maxMessages: 25, maxWaitTime: TimeSpan.FromSeconds(1));
            if (messages.Count == 0)
            {
                return;
            }

            foreach (var message in messages)
            {
                await receiver.CompleteMessageAsync(message);
            }
        }
    }

    private static string GetServiceBusConnectionString()
        => Environment.GetEnvironmentVariable("ASB_CONNECTION_STRING") ?? LocalServiceBusConnectionString;

    private static string CreateTestId()
        => Guid.NewGuid().ToString("N");

    private static string CreateHostId(string testId)
        => "afmsg" + testId.Substring(0, 27);

    private VerifySettings GetMessagingTriggerSettings()
    {
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddSimpleScrubber("aas.environment.runtime: .NET Core", "aas.environment.runtime: .NET");
        // Normalize emulator hostnames so snapshots are consistent across local and CI (Docker) environments
        settings.AddSimpleScrubber($"network.destination.name: {_eventHubsFixture.EventHubsHostname}", "network.destination.name: localhost");
        settings.AddSimpleScrubber("network.destination.name: azureservicebus-emulator", "network.destination.name: localhost");
        settings.AddSimpleScrubber($"server.address: {_eventHubsFixture.EventHubsHostname}", "server.address: localhost");
        settings.AddSimpleScrubber("server.address: azureservicebus-emulator", "server.address: localhost");
        // SpanLinks contain raw 128-bit trace IDs and trace state that change between runs
        settings.AddRegexScrubber(new Regex(@"TraceIdLow: \d+"), "TraceIdLow: 0");
        settings.AddRegexScrubber(new Regex(@"TraceIdHigh: \d+"), "TraceIdHigh: 0");
        settings.AddRegexScrubber(new Regex(@"TraceFlags: \d+"), "TraceFlags: 0");
        settings.AddRegexScrubber(new Regex(@"TraceState: [^\r\n]+"), "TraceState: scrubbed");
        return settings;
    }

    private async Task ResetEventHubCheckpointStore()
    {
        var container = new BlobContainerClient(_eventHubsFixture.AzuriteConnectionString, "azure-webjobs-eventhub");
        await container.CreateIfNotExistsAsync();

        var prefix = $"{_eventHubsFixture.EventHubsHostname}/{EventHubName}/{EventHubConsumerGroup}/";
        await foreach (var blob in container.GetBlobsAsync(prefix: prefix))
        {
            await container.DeleteBlobIfExistsAsync(blob.Name);
        }
    }
}

#endif
