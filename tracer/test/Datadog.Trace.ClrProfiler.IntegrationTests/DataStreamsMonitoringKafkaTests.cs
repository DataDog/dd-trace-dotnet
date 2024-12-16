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
    /// </summary>
    /// <param name="enableConsumerScopeCreation">Is the scope created manually or using built-in support</param>
    /// <param name="enableLegacyHeaders">Should legacy headers be enabled or not</param>
    [SkippableTheory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")]
    public async Task SubmitsDataStreams(bool enableConsumerScopeCreation, bool enableLegacyHeaders)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        SetEnvironmentVariable(ConfigurationKeys.KafkaCreateConsumerScopeEnabled, enableConsumerScopeCreation ? "1" : "0");
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.LegacyHeadersEnabled, enableLegacyHeaders ? "1" : "0");

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
}
