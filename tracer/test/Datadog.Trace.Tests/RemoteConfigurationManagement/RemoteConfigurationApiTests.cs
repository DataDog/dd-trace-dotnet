// <copyright file="RemoteConfigurationApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class RemoteConfigurationApiTests
{
    [Fact]
    public async Task WhenDiscoveryServiceDoesNotTriggerChange_GetConfigsReturnsNull()
    {
        var requestFactory = new TestRequestFactory();
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);

        var result = await api.GetConfigs(GetRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhenDiscoveryServiceDoesNotIndicateRcmAvailable_GetConfigsReturnsNull()
    {
        var requestFactory = new TestRequestFactory();
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);

        discoveryService.TriggerChange(configurationEndpoint: null);

        var result = await api.GetConfigs(GetRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhenConfigEndpointAvailable_SendsRequestToGetConfig()
    {
        var requestFactory = new TestRequestFactory();
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        var result = await api.GetConfigs(GetRequest());

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
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        var result = await api.GetConfigs(GetRequest());

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
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        var result = await api.GetConfigs(GetRequest());

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
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        await api.Invoking(x => x.GetConfigs(GetRequest()))
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
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        await api.Invoking(x => x.GetConfigs(GetRequest()))
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
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        var result = await api.GetConfigs(GetRequest());

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
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        var result = await api.GetConfigs(GetRequest());

        requestFactory.RequestsSent.Should().ContainSingle();

        AssertTargetsDeserialization(result);
        result.TargetFiles.Should().BeEmpty();
        result.ClientConfigs.Should().NotBeNullOrEmpty().And.NotContainNulls();
    }

    [Theory]
    [MemberData(nameof(RcmResponses.TargetsOnly), MemberType = typeof(RcmResponses))]
    public async Task WhenConfigEndpointReturnsValidContent_IncludesOnlyTargets_ReturnsDeserializedValues(string responseContent)
    {
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, responseContent: responseContent));
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        var result = await api.GetConfigs(GetRequest());

        requestFactory.RequestsSent.Should().ContainSingle();

        AssertTargetsDeserialization(result);
        result.TargetFiles.Should().BeEmpty();
        result.ClientConfigs.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenConfigEndpointReturnsValidContent_HandlesHugeResponse_ReturnsDeserializedValues()
    {
        var responseContent = RcmResponses.HugeConfig();
        var requestFactory = new TestRequestFactory(
            url => new TestApiRequest(url, responseContent: responseContent));
        var discoveryService = new DiscoveryServiceMock();
        var api = RemoteConfigurationApi.Create(requestFactory, discoveryService);
        discoveryService.TriggerChange();

        var result = await api.GetConfigs(GetRequest());

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
        var tracer = new RcmClientTracer(
            runtimeId: Guid.NewGuid().ToString(),
            tracerVersion: TracerConstants.ThreePartVersion,
            service: nameof(RemoteConfigurationApiTests),
            env: "RCM Test",
            appVersion: "1.0.0",
            tags: []);

        var state = new RcmClientState(
            rootVersion: 1,
            targetsVersion: 0,
            configStates: [], // e.g. first request
            hasError: false,
            error: null,
            backendClientState: backendClientStage);

        // make sure we test with something valid at least
        var rcm = new RcmSubscriptionManager();
        rcm.SetCapability(RcmCapabilitiesIndices.ApmTracingTracingEnabled, true);
        var capabilities = rcm.GetCapabilities();

        var client = new RcmClient(
            Guid.NewGuid().ToString(),
            products: [RcmProducts.TracerFlareInitiated, RcmProducts.TracerFlareRequested],
            tracer,
            state,
            capabilities);

        return new GetRcmRequest(client, cachedTargetFiles: []);
    }
}
