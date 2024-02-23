// <copyright file="DataStreamsMonitoringKafkaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.DataStreamsMonitoring;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
[Collection(nameof(KafkaTests.KafkaTestsCollection))]
[Trait("RequiresDockerDependency", "true")]
public class DataStreamsMonitoringKafkaTests : TestHelper
{
    public DataStreamsMonitoringKafkaTests(ITestOutputHelper output)
        : base("DataStreams.Kafka", output)
    {
        SetServiceVersion("1.0.0");
    }

    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <summary>
    /// This sample does a series of produces and consumes to create two pipelines:
    ///  - service -> topic 1 -> Consumer 1 -> topic 2 -> Consumer 2 -> topic 3 -> consumer 3
    ///  - service -> topic 2 -> Consumer 2 -> topic 3 -> consumer 3
    /// Each node (apart from 'service') in the pipelines above have a unique hash
    ///
    /// In mermaid (view at https://mermaid.live/), this looks like:
    /// sequenceDiagram
    ///    participant A as Root Service (12926600137239154356)
    ///    participant T1 as Topic 1 (2704081292861755358)
    ///    participant C1 as Consumer 1 (5289074475783863123)
    ///    participant T2a as Topic 2 (2821413369272395429)
    ///    participant C2a as Consumer 2 (9753735904472423641)
    ///    participant T3a as Topic 3 (5363062531028060751)
    ///    participant T2 as Topic 2 (246622801349204431)
    ///    participant C2 as Consumer 2 (3398817358352474903)
    ///    participant T3 as Topic 3 (16689539899325095461 )
    ///
    ///    A->>+T1: Produce
    ///    T1-->>-C1: Consume
    ///    C1->>+T2a: Produce
    ///    T2a-->>-C2a: Consume
    ///    C2a->>+T3a: Produce
    ///
    ///    A->>+T2: Produce
    ///    T2-->>-C2: Consume
    ///    C2->>+T3: Produce
    /// </summary>
    /// <param name="enableConsumerScopeCreation">Is the scope created manually or using built-in support</param>
    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public async Task SubmitsDataStreams(bool enableConsumerScopeCreation)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.KafkaCreateConsumerScopeEnabled, enableConsumerScopeCreation ? "1" : "0");

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

        using var processResult = await RunSampleAndWaitForExit(agent);

        using var assertionScope = new AssertionScope();
        var payload = MockDataStreamsPayload.Normalize(agent.DataStreams);
        // using span verifier to add all the default scrubbers
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddSimpleScrubber(TracerConstants.AssemblyVersion, "2.x.x.x");
        settings.AddDataStreamsScrubber();
        await Verifier.Verify(payload, settings)
                      .UseFileName($"{nameof(DataStreamsMonitoringKafkaTests)}.{nameof(SubmitsDataStreams)}")
                      .DisableRequireUniquePrefix();
    }

    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <summary>
    /// This sample tests a fan in + out scenario:
    ///  - service -> topic 1 -|                  | -> topic 2 -> Consumer 2
    ///  - service -> topic 1 -|---> Consumer 1  -| -> topic 2 -> Consumer 2
    ///  - service -> topic 1 -|                  | -> topic 2 -> Consumer 2
    ///
    /// It aims to replicate what happens when a bunch of messages are read at once,
    /// then processed in a loop before new outputs are produced.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public async Task HandlesBatchProcessing()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        // set variable to create short spans on receive instead of spans that last until the next consume
        SetEnvironmentVariable(ConfigurationKeys.KafkaCreateConsumerScopeEnabled, "0");

        using var agent = EnvironmentHelper.GetMockAgent(useTelemetry: true);

        using var processResult = await RunSampleAndWaitForExit(agent, "--batch-processing");

        using var assertionScope = new AssertionScope();

        var payload = MockDataStreamsPayload.Normalize(agent.DataStreams);

        // using span verifier to add all the default scrubbers
        var settings = VerifyHelper.GetSpanVerifierSettings();
        settings.AddSimpleScrubber(TracerConstants.AssemblyVersion, "2.x.x.x");
        settings.AddDataStreamsScrubber();
        await Verifier.Verify(payload, settings)
                      .UseFileName($"{nameof(DataStreamsMonitoringKafkaTests)}.{nameof(HandlesBatchProcessing)}")
                      .DisableRequireUniquePrefix();
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public async Task WhenDisabled_DoesNotSubmitDataStreams()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "0");

        using var agent = EnvironmentHelper.GetMockAgent();
        using var processResult = await RunSampleAndWaitForExit(agent);

        using var assertionScope = new AssertionScope();
        // We don't expect any streams here, so no point waiting for ages
        var dataStreams = agent.WaitForDataStreams(2, timeoutInMilliseconds: 2_000);
        dataStreams.Should().BeEmpty();
    }

    [SkippableFact]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public async Task WhenNotSupported_DoesNotSubmitDataStreams()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");

        using var agent = EnvironmentHelper.GetMockAgent();
        agent.Configuration = new MockTracerAgent.AgentConfiguration { Endpoints = Array.Empty<string>() };
        using var processResult = await RunSampleAndWaitForExit(agent);

        using var assertionScope = new AssertionScope();
        var dataStreams = agent.DataStreams;
        dataStreams.Should().BeEmpty();
    }
}
