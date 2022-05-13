// <copyright file="AgentTelemetryTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Transports;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry.Transports;

public class AgentTelemetryTransportTests
{
    private static readonly Uri BaseEndpoint = new Uri("http://localhost");

    [Theory]
    [InlineData("7.26.5", (int)TelemetryPushResult.FatalErrorDontRetry)] // version too small
    [InlineData("7.34.0", (int)TelemetryPushResult.TransientFailure)] // first supported version
    [InlineData("8.29.6", (int)TelemetryPushResult.TransientFailure)] // some version in the future
    [InlineData("", (int)TelemetryPushResult.FatalErrorDontRetry)] // missing (v. old)
    [InlineData(null, (int)TelemetryPushResult.FatalErrorDontRetry)] // missing (v. old)
    [InlineData("N/A", (int)TelemetryPushResult.FatalErrorDontRetry)] // mangled (somehow)
    public async Task ChecksAgentVersionHeaderOnError(string agentVersionHeader, int expectedResult)
    {
        var response = new TestApiRequestFactory.TestApiResponse(
            statusCode: 500,
            body: "{}",
            contentType: "application/json",
            headers: new Dictionary<string, string> { { AgentHttpHeaderNames.AgentVersion, agentVersionHeader } });

        var requestFactory = new TestApiRequestFactory(
            BaseEndpoint,
            x => new TestApiRequestFactory.TestApiRequest(x, (_, _) => response));

        var transport = new AgentTelemetryTransport(requestFactory);

        transport.DetectedAgentVersion.Should().BeNull();

        var result = await transport.PushTelemetry(GetSampleData());

        transport.DetectedAgentVersion.Should().Be(agentVersionHeader ?? string.Empty);
        result.Should().Be((TelemetryPushResult)expectedResult);
    }

    [Fact]
    public async Task DoesntCheckVersionOnSuccess()
    {
        var requestFactory = new TestApiRequestFactory(BaseEndpoint);
        var transport = new AgentTelemetryTransport(requestFactory);

        var result = await transport.PushTelemetry(GetSampleData());

        transport.DetectedAgentVersion.Should().BeNull();
        result.Should().Be(TelemetryPushResult.Success);
    }

    private static TelemetryData GetSampleData()
    {
        return new TelemetryData(
            requestType: TelemetryRequestTypes.AppHeartbeat,
            tracerTime: 1234,
            runtimeId: "some-value",
            seqId: 23,
            application: new ApplicationTelemetryData(
                serviceName: "TelemetryTransportTests",
                env: "TracerTelemetryTest",
                tracerVersion: TracerConstants.AssemblyVersion,
                languageName: "dotnet",
                languageVersion: "1.2.3"),
            host: new HostTelemetryData(),
            payload: null);
    }
}
