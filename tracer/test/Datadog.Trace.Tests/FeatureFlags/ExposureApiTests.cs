// <copyright file="ExposureApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using Moq;
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
        // The send gates control shutdown in this test; a finite shutdown timeout would race
        // the test's continuations. The bounded shutdown path is covered separately below.
        using var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), Timeout.InfiniteTimeSpan);
        Task? dispose = null;

        try
        {
            api.SendExposure(CreateExposure("first"));
            (await Task.WhenAny(firstSendStarted.Task, Task.Delay(TimeSpan.FromSeconds(3))))
               .Should().BeSameAs(firstSendStarted.Task);

            api.SendExposure(CreateExposure("second"));
            // Dispose blocks synchronously, so keep it off the pool that runs the send/test continuations.
            dispose = Task.Factory.StartNew(api.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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
            finalRequest.Endpoint.AbsolutePath.Should().Be("/evp_proxy/v4/api/v2/exposures");
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
        var logger = CreateDebugLogger();
        using var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(25), logger.Object);
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
            ArgumentsOf(logger, "may be lost").Should().Equal(25d);
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

    [Fact]
    public void ReportsWhatHappensToEachExposureThroughShutdown()
    {
        var logger = CreateDebugLogger();
        var local = new TestRequestFactory(new Uri("http://agent:8126/"));
        using var transport = CreateLocalTransport(local, logger.Object);
        var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), TimeSpan.FromSeconds(10), logger.Object);

        api.SendExposure(CreateExposure("first"));
        api.SendExposure(CreateExposure("first"));
        api.Dispose();
        api.SendExposure(CreateExposure("after-dispose"));

        ArgumentsOf(logger, "queued for the next flush").Should().Equal("first", "allocation", "variant", "subject");
        ArgumentsOf(logger, "not queued").Should().Equal("first", "subject");
        ArgumentsOf(logger, "sending a batch of").Should().Equal(1);
        ArgumentsOf(logger, "accepted by the Agent EVP proxy").Should().Equal(FeatureFlagsEvpTransport.ExposureIntakePath, FeatureFlagsEvpTransport.EventPlatformProxyV4, 200);
        ArgumentsOf(logger, "final exposure flush").Should().ContainSingle();
        ArgumentsOf(logger, "dropped: exposure reporting has stopped").Should().Equal("after-dispose");
        logger.Verify(l => l.Debug("ExposureApi::UpdateContext -> Applying settings", It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        logger.Verify(l => l.Debug("ExposureApi::SendLoopAsync -> Enter", It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        logger.Verify(l => l.Debug("ExposureApi::SendLoopAsync -> Exit", It.IsAny<int>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void SendExposureReturnsTheDeduplicationVerdict()
    {
        var local = new TestRequestFactory(new Uri("http://agent:8126/"));
        using var transport = CreateLocalTransport(local);
        using var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(10));

        api.SendExposure(CreateExposure("flag")).Should().BeFalse("the first exposure of a subject is new");
        api.SendExposure(CreateExposure("flag")).Should().BeTrue("the same exposure again is a cache hit");

        api.SendExposure(CreateExposure("flag", variant: "variant-b")).Should().BeFalse("a changed variant is a new exposure");
        api.SendExposure(CreateExposure("flag")).Should().BeFalse("switching back to an earlier variant is also a change");

        api.SendExposure(CreateExposure("flag", serialId: 42)).Should().BeFalse("a changed serial id is a new exposure");
        api.SendExposure(CreateExposure("flag", serialId: 42)).Should().BeTrue("the new serial id repeats as a cache hit");

        api.SendExposure(CreateExposure("other-flag")).Should().BeFalse("another flag is a new exposure");
    }

    [Fact]
    public async Task SendExposureWithDatadogLoggingDisabledRecordsButSendsNothing()
    {
        var local = new TestRequestFactory(new Uri("http://agent:8126/"));
        using var transport = CreateLocalTransport(local);
        var settings = new TracerSettings(
            new NameValueConfigurationSource(new NameValueCollection
            {
                ["DD_FEATURE_FLAGS_EXPOSURES_DATADOG_LOGGING_ENABLED"] = "false",
            }));
        using var api = new ExposureApi(settings, transport, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(1));

        api.SendExposure(CreateExposure("flag")).Should().BeFalse();
        api.SendExposure(CreateExposure("flag")).Should().BeTrue("the cache still records with logging off");

        await Task.Delay(TimeSpan.FromMilliseconds(50));
        local.RequestsSent.Should().BeEmpty("no exposure reaches Datadog with the setting off");
    }

    [Fact]
    public void SendExposureWithDatadogLoggingEnabledSendsNewExposuresOnly()
    {
        // The send loop can flush the first exposure before the second call, so every batch is counted.
        // The factory only uses the given callbacks for the first requests, one callback per request.
        var payloads = new ConcurrentQueue<string>();
        var local = new TestRequestFactory(new Uri("http://agent:8126/"), Enumerable.Repeat<Func<Uri, TestApiRequest>>(uri => new CollectingJsonApiRequest(uri, payloads), 4).ToArray());
        using var transport = CreateLocalTransport(local);
        var api = new ExposureApi(CreateSettings(), transport, TimeSpan.FromHours(1), TimeSpan.FromSeconds(3));

        api.SendExposure(CreateExposure("flag")).Should().BeFalse();
        api.SendExposure(CreateExposure("flag")).Should().BeTrue();
        api.Dispose();

        payloads.Should().NotBeEmpty("the new exposure is sent");
        payloads.Sum(json => JObject.Parse(json)["exposures"]!.Count()).Should().Be(1, "a repeat is not sent");
    }

    private static Mock<IDatadogLogger> CreateDebugLogger()
    {
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        return logger;
    }

    // The values handed to the single log call whose message contains the fragment.
    private static List<object?> ArgumentsOf(Mock<IDatadogLogger> logger, string templateFragment)
        => logger.Invocations
                 .Select(i => i.Method.GetParameters().Zip(i.Arguments, (parameter, argument) => (parameter.Name, argument)).ToList())
                 .Where(call => call.Any(p => p.Name == "messageTemplate" && ((string)p.argument!).Contains(templateFragment)))
                 .Select(call => call.Where(p => p.Name is not ("sourceLine" or "sourceFile" or "messageTemplate"))
                                     .Select(p => (object?)p.argument)
                                     .ToList())
                 .Should().ContainSingle().Subject;

    private static FeatureFlagsEvpTransport CreateLocalTransport(TestRequestFactory local, IDatadogLogger? logger = null)
        => new(
            FeatureFlagsSource.Agentless,
            local,
            directRequestFactory: null,
            discoveryService: new DiscoveryServiceMock(),
            initialLocalProxyEndpoint: FeatureFlagsEvpTransport.EventPlatformProxyV4,
            logger: logger);

    private static ExposureEvent CreateExposure(string flag, string variant = "variant", long? serialId = null)
        => new(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new Allocation("allocation"),
            new Flag(flag),
            new Variant(variant),
            new Subject("subject", new Dictionary<string, object?>()),
            serialId);

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

    private sealed class CollectingJsonApiRequest(Uri endpoint, ConcurrentQueue<string> payloads) : RecordingJsonApiRequest(endpoint)
    {
        public override Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
        {
            payloads.Enqueue(JsonConvert.SerializeObject(payload, settings));
            return base.PostAsJsonAsync(payload, compression, settings);
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
