// <copyright file="ExposureApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.TestHelpers.TransportHelpers;
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
        var finalSendStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFinalSend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var local = new TestRequestFactory(
            new Uri("http://agent:8126/"),
            uri => new BlockingApiRequest(uri, firstSendStarted, releaseFirstSend),
            uri => new BlockingApiRequest(uri, finalSendStarted, releaseFinalSend));
        using var transport = CreateLocalTransport(local);
        // Allow for thread-pool scheduling delays while the test coordinates the blocked sends.
        using var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1));
        Task? dispose = null;

        try
        {
            api.SendExposure(CreateExposure("first"));
            (await Task.WhenAny(firstSendStarted.Task, Task.Delay(TimeSpan.FromSeconds(3))))
               .Should().BeSameAs(firstSendStarted.Task);

            api.SendExposure(CreateExposure("second"));
            dispose = Task.Run(api.Dispose);
            releaseFirstSend.TrySetResult(true);

            (await Task.WhenAny(finalSendStarted.Task, Task.Delay(TimeSpan.FromSeconds(3))))
               .Should().BeSameAs(finalSendStarted.Task);
            (await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromMilliseconds(100))))
               .Should().NotBeSameAs(dispose, "Dispose must wait while the final batch is still sending");

            releaseFinalSend.TrySetResult(true);
            (await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(3)))).Should().BeSameAs(dispose);
            await dispose;

            local.RequestsSent.Should().HaveCount(2, "the second exposure is sent by the shutdown flush");
            var finalRequest = local.RequestsSent[1].Should().BeOfType<BlockingApiRequest>().Subject;
            finalRequest.Endpoint.AbsolutePath.Should().Be("/evp_proxy/v2/api/v2/exposures");
            finalRequest.Compression.Should().Be(MultipartCompression.GZip);
            var payload = JObject.Parse(finalRequest.PayloadJson);
            payload["context"]!["service"]!.Value<string>().Should().NotBeNullOrEmpty();
            payload["exposures"]!.Should().ContainSingle();
            payload["exposures"]![0]!["flag"]!["key"]!.Value<string>().Should().Be("second");
        }
        finally
        {
            releaseFirstSend.TrySetResult(true);
            releaseFinalSend.TrySetResult(true);
            if (dispose is not null)
            {
                await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(3)));
            }
        }
    }

    [Fact]
    public async Task DisposeHasABoundedWaitWhenTheNetworkSendDoesNotFinish()
    {
        var sendStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var local = new TestRequestFactory(
            new Uri("http://agent:8126/"),
            uri => new BlockingApiRequest(uri, sendStarted, releaseSend));
        using var transport = CreateLocalTransport(local);
        using var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(25));
        Task? dispose = null;

        try
        {
            api.SendExposure(CreateExposure("first"));
            (await Task.WhenAny(sendStarted.Task, Task.Delay(TimeSpan.FromSeconds(3))))
               .Should().BeSameAs(sendStarted.Task);

            dispose = Task.Run(api.Dispose);
            (await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(3))))
               .Should().BeSameAs(dispose, "shutdown must finish even while the request remains blocked");
            await dispose;
            releaseSend.Task.IsCompleted.Should().BeFalse();
        }
        finally
        {
            releaseSend.TrySetResult(true);
            if (dispose is not null)
            {
                await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(3)));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposeDoesNotPropagateSendLoopFailure(bool canceled)
    {
        var local = new TestRequestFactory(new Uri("http://agent:8126/"));
        using var transport = CreateLocalTransport(local);
        using var api = new ExposureApi(CreateSettings(), transport);
        var sendLoop = canceled
                           ? Task.FromCanceled(new CancellationToken(canceled: true))
                           : Task.FromException(new InvalidOperationException("send loop failed"));
        typeof(ExposureApi).GetField("_sendLoopTask", BindingFlags.Instance | BindingFlags.NonPublic)!
                           .SetValue(api, sendLoop);

        Action dispose = api.Dispose;

        dispose.Should().NotThrow();
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
        => new(local);

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
        TaskCompletionSource<bool> releaseSend) : RecordingJsonApiRequest(endpoint)
    {
        public override async Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
        {
            sendStarted.TrySetResult(true);
            await releaseSend.Task.ConfigureAwait(false);
            return await base.PostAsJsonAsync(payload, compression, settings).ConfigureAwait(false);
        }
    }

    private class RecordingJsonApiRequest(Uri endpoint) : TestApiRequest(endpoint)
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
