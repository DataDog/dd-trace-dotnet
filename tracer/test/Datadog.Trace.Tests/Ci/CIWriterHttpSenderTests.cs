// <copyright file="CIWriterHttpSenderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers.TransportHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class CIWriterHttpSenderTests
{
    [Fact]
    public async Task DoesNotRetryAfterApiKeyTransportRejection()
    {
        var settings = new TestOptimizationSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance);
        settings.SetAgentlessConfiguration(enabled: true, apiKey: "test-key", agentlessUrl: "https://example.com");
        var payload = new CITestCyclePayload(settings, CIFormatterResolver.Instance);
        var requestFactory = new TestRequestFactory(new Uri("https://example.com"), x => new UnsafeApiKeyTransportRequest(x));
        var sender = new CIWriterHttpSender(requestFactory);

        await sender.SendPayloadAsync(payload);
        await sender.SendPayloadAsync(payload);

        requestFactory.RequestsSent.Should().ContainSingle();
    }

    private sealed class UnsafeApiKeyTransportRequest : TestApiRequest
    {
        public UnsafeApiKeyTransportRequest(Uri endpoint)
            : base(endpoint)
        {
        }

        public override Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
            => Task.FromException<IApiResponse>(new ApiKeyHttpTransportException("Unsafe API-key transport."));
    }
}
