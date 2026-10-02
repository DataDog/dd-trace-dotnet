// <copyright file="ExposureApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class ExposureApiTests
{
    [Fact]
    public async Task DisposeFlushesEventsQueuedWhileAnEarlierBatchIsSending()
    {
        var firstSendStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstSend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var local = new TestRequestFactory(
            new Uri("http://agent:8126/"),
            uri => new BlockingApiRequest(uri, firstSendStarted, releaseFirstSend),
            uri => new RecordingJsonApiRequest(uri));
        var transport = CreateLocalTransport(local);
        var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), TimeSpan.FromSeconds(2));

        api.SendExposure(CreateExposure("first"));
        (await Task.WhenAny(firstSendStarted.Task, Task.Delay(TimeSpan.FromSeconds(1))))
           .Should().BeSameAs(firstSendStarted.Task);

        api.SendExposure(CreateExposure("second"));
        var dispose = Task.Run(api.Dispose);
        releaseFirstSend.TrySetResult(true);

        (await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(3)))).Should().BeSameAs(dispose);
        await dispose;

        local.RequestsSent.Should().HaveCount(2, "the second exposure is sent by the shutdown flush");
        var finalRequest = local.RequestsSent[1].Should().BeOfType<RecordingJsonApiRequest>().Subject;
        finalRequest.Endpoint.AbsolutePath.Should().Be("/evp_proxy/v4/api/v2/exposures");
        finalRequest.Compression.Should().Be(MultipartCompression.GZip);
        var payload = JObject.Parse(finalRequest.PayloadJson);
        payload["context"]!["service"]!.Value<string>().Should().NotBeNullOrEmpty();
        payload["exposures"]!.Should().ContainSingle();
        payload["exposures"]![0]!["flag"]!["key"]!.Value<string>().Should().Be("second");
    }

    [Fact]
    public async Task DisposeHasABoundedWaitWhenTheNetworkSendDoesNotFinish()
    {
        var sendStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var local = new TestRequestFactory(
            new Uri("http://agent:8126/"),
            uri => new BlockingApiRequest(uri, sendStarted, releaseSend));
        var transport = CreateLocalTransport(local);
        var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(25));

        api.SendExposure(CreateExposure("first"));
        (await Task.WhenAny(sendStarted.Task, Task.Delay(TimeSpan.FromSeconds(1))))
           .Should().BeSameAs(sendStarted.Task);

        var stopwatch = Stopwatch.StartNew();
        api.Dispose();
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        releaseSend.TrySetResult(true);
    }

    [Fact]
    public async Task SendExposureAfterDisposeIsIgnored()
    {
        var local = new TestRequestFactory(new Uri("http://agent:8126/"));
        var transport = CreateLocalTransport(local);
        var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(1));

        api.Dispose();
        api.SendExposure(CreateExposure("after-dispose"));
        await Task.Delay(TimeSpan.FromMilliseconds(20));

        local.RequestsSent.Should().BeEmpty();
    }

    private static FeatureFlagsEvpTransport CreateLocalTransport(TestRequestFactory local)
        => new(
            FeatureFlagsSource.Agentless,
            local,
            directRequestFactory: null,
            discoveryService: new DiscoveryServiceMock(),
            initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4);

    private static ExposureEvent CreateExposure(string flag)
        => new(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new Allocation("allocation"),
            new Flag(flag),
            new Variant("variant"),
            new Subject("subject", new Dictionary<string, object?>()));

    private static TracerSettings CreateSettings()
        => new(new NameValueConfigurationSource(new NameValueCollection()));

    private sealed class BlockingApiRequest(
        Uri endpoint,
        TaskCompletionSource<bool> sendStarted,
        TaskCompletionSource<bool> releaseSend) : TestApiRequest(endpoint)
    {
        public override async Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
        {
            sendStarted.TrySetResult(true);
            await releaseSend.Task.ConfigureAwait(false);
            return await base.PostAsJsonAsync(payload, compression, settings).ConfigureAwait(false);
        }
    }

    private sealed class RecordingJsonApiRequest(Uri endpoint) : TestApiRequest(endpoint)
    {
        public string PayloadJson { get; private set; } = string.Empty;

        public MultipartCompression Compression { get; private set; }

        public override Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
        {
            PayloadJson = JsonConvert.SerializeObject(payload, settings);
            Compression = compression;
            return base.PostAsJsonAsync(payload, compression, settings);
        }
    }
}
