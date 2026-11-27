// <copyright file="RemoteConfigurationApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RemoteConfigurationApiTests
{
    private const string ConfigEndpoint = "configurationEndpoint";

#if NET6_0_OR_GREATER
    [Fact]
    public async Task CancelsPendingNamedPipeResponse()
    {
        var pipeName = Path.GetRandomFileName();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var factory = new HttpStreamRequestFactory(
            new NamedPipeClientStreamFactory(pipeName, timeoutMs: 5000),
            new DatadogHttpClient(MinimalAgentHeaderHelper.Instance),
            new Uri("http://localhost"));
        var api = new RemoteConfigurationApi<IApiRequest>(factory);

        var poll = api.GetConfigs(GetRequest(), ConfigEndpoint, cancellation.Token);
        await pipe.WaitForConnectionAsync(timeout.Token);
        using var reader = new StreamReader(pipe, leaveOpen: true);
        var request = await reader.ReadLineAsync().WaitAsync(timeout.Token);
        request.Should().StartWith($"POST /{ConfigEndpoint} ");
        string header;
        do
        {
            header = await reader.ReadLineAsync().WaitAsync(timeout.Token);
        }
        while (!string.IsNullOrEmpty(header));

        header.Should().BeEmpty();
        var responseHeaders = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n"u8.ToArray();
        await pipe.WriteAsync(responseHeaders, timeout.Token);
        poll.IsCompleted.Should().BeFalse();

        cancellation.Cancel();

        (await poll.WaitAsync(timeout.Token)).Should().BeNull();
    }
#endif

    [Fact]
    public async Task WhenConfigEndpointAvailable_SendsRequestToGetConfig()
    {
        var requestFactory = new TestRequestFactory();
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        var result = await api.GetConfigs(GetRequest(), ConfigEndpoint);

        requestFactory.RequestsSent.Should()
                      .ContainSingle()
                      .Which.ContentType.Should()
                      .Be(MimeTypes.Json);
    }

    [Theory]
    [InlineData(404, "")]
    [InlineData(404, "Not available")]
    [InlineData(400, "")]
    [InlineData(400, "Not available")]
    [InlineData(401, "")]
    [InlineData(401, "Not available")]
    [InlineData(500, "")]
    [InlineData(500, "Not available")]
    [InlineData(503, "")]
    [InlineData(503, "Not available")]
    // Not bothering testing 3xx responses here
    public async Task WhenEndpointReturnsError_ReturnsNull(int statusCode, string responseContent)
    {
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, statusCode: statusCode, responseContent: responseContent));
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        var result = await api.GetConfigs(GetRequest(), ConfigEndpoint);

        requestFactory.RequestsSent.Should().ContainSingle();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    public async Task WhenEndpointReturnsEmptyContent_ReturnsNull(int statusCode)
    {
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, statusCode: statusCode, responseContent: string.Empty));
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        var result = await api.GetConfigs(GetRequest(), ConfigEndpoint);

        requestFactory.RequestsSent.Should().ContainSingle();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("r")]
    [InlineData("remote_configuration")]
    [InlineData("}")]
    public async Task WhenEndpointReturnsInvalidContent_InvalidFirstCharacter_Throws(string responseContent)
    {
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, responseContent: responseContent));
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        await api.Invoking(x => x.GetConfigs(GetRequest(), ConfigEndpoint))
                 .Should()
                 .ThrowAsync<RemoteConfigurationDeserializationException>()
                 .WithInnerExceptionExactly<RemoteConfigurationDeserializationException, JsonException>()
                 .WithMessage($"Unexpected character encountered while parsing value: {responseContent[0]}. Path '', line 0, position 0. Original content length {responseContent.Length} and content: '{responseContent}'")
                 .WithInnerExceptionExactly(typeof(JsonReaderException));
    }

    [Theory]
    [InlineData("""{"something": {"that": "is", "almost": "json"}, "Except": Not}""")]
    [InlineData("""{"something": {"that": "is", "almost": "json"}, "Except": "Not" quite}""")]
    [InlineData("""{"close": "but": "no": "cigar"}""")]
    [InlineData("""{"ooooh": "nearly", "made": "it" """)]
    public async Task WhenEndpointReturnsInvalidContent_InvalidLaterValues_Throws(string responseContent)
    {
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, responseContent: responseContent));
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        await api.Invoking(x => x.GetConfigs(GetRequest(), ConfigEndpoint))
                 .Should()
                 .ThrowAsync<RemoteConfigurationDeserializationException>()
                 .WithInnerExceptionExactly<RemoteConfigurationDeserializationException, JsonException>()
                 .WithMessage($"* Original content length {responseContent.Length} and content: '{responseContent}'")
                 .WithInnerException(typeof(JsonException));
    }

    [Theory]
    [MemberData(nameof(RcmResponses.TargetsTargetFilesClientConfigs), MemberType = typeof(RcmResponses))]
    public async Task WhenConfigEndpointReturnsValidContent_IncludesAllConfig_ReturnsDeserializedValues(string responseContent)
    {
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, responseContent: responseContent));
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        var result = await api.GetConfigs(GetRequest(), ConfigEndpoint);

        requestFactory.RequestsSent.Should().ContainSingle();

        // basic check of deserialization
        AssertTargetsDeserialization(result);
        result.TargetFiles.Should().NotBeNullOrEmpty().And.NotContainNulls();
        result.ClientConfigs.Should().NotBeNullOrEmpty().And.NotContainNulls();
    }

    [Theory]
    [MemberData(nameof(RcmResponses.TargetsClientConfigsOnly), MemberType = typeof(RcmResponses))]
    public async Task WhenConfigEndpointReturnsValidContent_IncludesOnlyTargetsAndClientConfigs_ReturnsDeserializedValues(string responseContent)
    {
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, responseContent: responseContent));
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        var result = await api.GetConfigs(GetRequest(), ConfigEndpoint);

        requestFactory.RequestsSent.Should().ContainSingle();

        AssertTargetsDeserialization(result);
        result.TargetFiles.Should().BeNullOrEmpty();
        result.ClientConfigs.Should().NotBeNullOrEmpty().And.NotContainNulls();
    }

    [Theory]
    [MemberData(nameof(RcmResponses.TargetsOnly), MemberType = typeof(RcmResponses))]
    public async Task WhenConfigEndpointReturnsValidContent_IncludesOnlyTargets_ReturnsDeserializedValues(string responseContent)
    {
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, responseContent: responseContent));
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        var result = await api.GetConfigs(GetRequest(), ConfigEndpoint);

        requestFactory.RequestsSent.Should().ContainSingle();

        AssertTargetsDeserialization(result);
        result.TargetFiles.Should().BeNullOrEmpty();
        result.ClientConfigs.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task WhenConfigEndpointReturnsValidContent_HandlesHugeResponse_ReturnsDeserializedValues()
    {
        var responseContent = RcmResponses.HugeConfig();
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, responseContent: responseContent));
        var api = new RemoteConfigurationApi<IApiRequest>(requestFactory);

        var result = await api.GetConfigs(GetRequest(), ConfigEndpoint);

        requestFactory.RequestsSent.Should().ContainSingle();

        AssertTargetsDeserialization(result);
        result.TargetFiles.Should().NotBeNullOrEmpty().And.NotContainNulls();
        result.ClientConfigs.Should().NotBeNullOrEmpty().And.NotContainNulls();
    }

    private static void AssertTargetsDeserialization(GetRcmResponse result)
    {
        result.Should().NotBeNull();
        var targets = result?.Targets;
        targets.Should().NotBeNull();
        targets!.Signed.Should().NotBeNull();
        var signed = targets.Signed;
        signed.Version.Should().BeGreaterThan(0);
        signed.Custom.Should().NotBeNull();
    }

    private static GetRcmRequest GetRequest(string backendClientStage = null)
    {
        // We don't really care about this being "real" data, as long as it has the right shape,
        // but we'll keep it reasonable here for the sake of it
        var tracer = RcmClientTracer.Create(
            runtimeId: Guid.NewGuid().ToString(),
            tracerVersion: TracerConstants.SemanticVersion,
            service: nameof(RemoteConfigurationApiTests),
            env: "RCM Test",
            appVersion: "1.0.0",
            globalTags: ReadOnlyDictionary.Empty,
            processTags: ["a.b:c", "x.y:z"]);

        var state = new RcmClientState
        {
            RootVersion = 1,
            TargetsVersion = 0,
            BackendClientState = backendClientStage
        };

        // make sure we test with something valid at least
        var rcm = new RcmSubscriptionManager();
        rcm.SetCapability(RcmCapabilitiesIndices.ApmTracingTracingEnabled, true);
        var capabilities = rcm.GetCapabilities();

        var client = new RcmClient(Guid.NewGuid().ToString(), state)
        {
            Products = [RcmProducts.TracerFlareInitiated, RcmProducts.TracerFlareRequested],
            ClientTracer = tracer,
            Capabilities = capabilities,
        };

        return new GetRcmRequest(client);
    }
}
