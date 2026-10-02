// <copyright file="FlagEvaluationAgentSenderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.FeatureFlags;

[Collection(nameof(WebRequestCollection))]
public class FlagEvaluationAgentSenderTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(202)]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(405)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task SendsEachBatchOnceAndDisposesResponse(int statusCode)
    {
        var factory = new Mock<IApiRequestFactory>(MockBehavior.Strict);
        var request = new Mock<IApiRequest>(MockBehavior.Strict);
        var response = new Mock<IApiResponse>(MockBehavior.Strict);
        response.SetupGet(x => x.StatusCode).Returns(statusCode);
        response.Setup(x => x.Dispose());
        var uri = new Uri("http://127.0.0.1:8126/evp_proxy/v2/api/v2/flagevaluation");
        var payload = new ArraySegment<byte>(new byte[] { 1, 2, 3 });
        factory.Setup(x => x.GetEndpoint("evp_proxy/v2/api/v2/flagevaluation")).Returns(uri);
        factory.Setup(x => x.Create(uri)).Returns(request.Object);
        request.Setup(x => x.PostAsync(payload, "application/json", "gzip")).ReturnsAsync(response.Object);

        using var sender = new FlagEvaluationAgentSender(factory.Object);
        await sender.SendCompressedAsync(payload);

        request.Verify(x => x.PostAsync(payload, "application/json", "gzip"), Times.Once);
        response.Verify(x => x.Dispose(), Times.Once);
        response.Verify(x => x.GetStreamAsync(), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSendIsNotReplayedAndNextBatchStillWorks(bool timeout)
    {
        var factory = new Mock<IApiRequestFactory>();
        var request = new Mock<IApiRequest>();
        var response = new Mock<IApiResponse>();
        response.SetupGet(x => x.StatusCode).Returns(202);
        var uri = new Uri("http://127.0.0.1:8126/evp_proxy/v2/api/v2/flagevaluation");
        factory.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns(uri);
        factory.Setup(x => x.Create(uri)).Returns(request.Object);
        Exception error = timeout ? new TaskCanceledException("private-canary") : new IOException("private-canary");
        request.SetupSequence(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", "gzip"))
               .ThrowsAsync(error)
               .ReturnsAsync(response.Object);

        using var sender = new FlagEvaluationAgentSender(factory.Object);
        var first = new ArraySegment<byte>(new byte[] { 1 });
        var second = new ArraySegment<byte>(new byte[] { 2 });
        await sender.SendCompressedAsync(first);
        await sender.SendCompressedAsync(second);

        request.Verify(x => x.PostAsync(first, "application/json", "gzip"), Times.Once);
        request.Verify(x => x.PostAsync(second, "application/json", "gzip"), Times.Once);
        response.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeDoesNotInterruptInFlightSendAndPreventsNewSends()
    {
        var factory = new Mock<IApiRequestFactory>(MockBehavior.Strict);
        var request = new Mock<IApiRequest>(MockBehavior.Strict);
        var response = new Mock<IApiResponse>();
        response.SetupGet(x => x.StatusCode).Returns(202);
        var pending = new TaskCompletionSource<IApiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var uri = new Uri("http://127.0.0.1:8126/evp_proxy/v2/api/v2/flagevaluation");
        factory.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns(uri);
        factory.Setup(x => x.Create(uri)).Returns(request.Object);
        request.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", "gzip")).Returns(pending.Task);

        using var sender = new FlagEvaluationAgentSender(factory.Object);
        var send = sender.SendCompressedAsync(new ArraySegment<byte>(new byte[] { 1 }));
        sender.Dispose();
        sender.Dispose();
        send.IsCompleted.Should().BeFalse();
        await sender.SendCompressedAsync(new ArraySegment<byte>(new byte[] { 2 }));
        pending.SetResult(response.Object);
        await send;

        request.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", "gzip"), Times.Once);
        response.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task StreamHeadersCarryProducerIdentityWithoutCredentials()
    {
        var helper = FlagEvaluationAgentHeaderHelper.Instance;
        helper.DefaultHeaders.Should().Contain(new KeyValuePair<string, string>("X-Datadog-EVP-Subdomain", "event-platform-intake"));
        helper.DefaultHeaders.Should().Contain(new KeyValuePair<string, string>("DD-EVP-ORIGIN", "dd-trace-dotnet"));
        helper.DefaultHeaders.Should().Contain(new KeyValuePair<string, string>("DD-EVP-ORIGIN-VERSION", TracerConstants.ThreePartVersion));
        var request = new HttpRequest("POST", "localhost", "/evp_proxy/v2/api/v2/flagevaluation", new HttpHeaders(), new BufferContent(new ArraySegment<byte>(new byte[] { 1 })));
        using var text = new StringWriter();
        await helper.WriteLeadingHeaders(request, text);
        text.ToString().Should().Contain("X-Datadog-EVP-Subdomain: event-platform-intake\r\n")
            .And.Contain("DD-EVP-ORIGIN: dd-trace-dotnet\r\n")
            .And.Contain("DD-EVP-ORIGIN-VERSION: " + TracerConstants.ThreePartVersion + "\r\n")
            .And.NotContain("DD-API-KEY", "the event sender must not forward configuration-fetch credentials");
    }

    [Theory]
    [InlineData("remote_config")]
    [InlineData("agentless")]
    public async Task ProductionFactorySendsGzipToAgentWithoutConfigurationApiKey(string source)
    {
        using var agent = MockTracerAgent.Create(output);
        var received = new List<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Add(args.Value);
        var settings = CreateSettings($"http://127.0.0.1:{agent.Port}", source);
        using var sender = new FlagEvaluationAgentSender(settings.Manager.InitialExporterSettings);
        await sender.SendCompressedAsync(Compress("{\"events\":[]}"));

        received.Should().ContainSingle();
        received[0].PathAndQuery.Should().Be("/evp_proxy/v2/api/v2/flagevaluation");
        received[0].Headers["Content-Encoding"].Should().Be("gzip");
        received[0].Headers["Content-Type"].Should().Be("application/json");
        received[0].Headers["X-Datadog-EVP-Subdomain"].Should().Be("event-platform-intake");
        received[0].Headers["DD-EVP-ORIGIN"].Should().Be("dd-trace-dotnet");
        received[0].Headers["DD-EVP-ORIGIN-VERSION"].Should().Be(TracerConstants.ThreePartVersion);
        received[0].Headers["DD-API-KEY"].Should().BeNull();
        received[0].Headers["Authorization"].Should().BeNull();
        received[0].BodyInJson.Should().Be("{\"events\":[]}");
    }

    [Fact]
    public async Task ExporterChangeUsesNewAgentWithoutInterruptingInFlightSend()
    {
        var factory = new Mock<IApiRequestFactory>();
        var request = new Mock<IApiRequest>();
        var response = new Mock<IApiResponse>();
        response.SetupGet(x => x.StatusCode).Returns(202);
        var pending = new TaskCompletionSource<IApiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var uri = new Uri("http://127.0.0.1:8126/evp_proxy/v2/api/v2/flagevaluation");
        factory.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns(uri);
        factory.Setup(x => x.Create(uri)).Returns(request.Object);
        request.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", "gzip")).Returns(pending.Task);
        using var sender = new FlagEvaluationAgentSender(factory.Object);
        var originalSend = sender.SendCompressedAsync(Compress("{}"));
        using var agent = MockTracerAgent.Create(output);
        var received = new List<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Add(args.Value);

        sender.UpdateExporterSettings(CreateSettings($"http://127.0.0.1:{agent.Port}").Manager.InitialExporterSettings);
        await sender.SendCompressedAsync(Compress("{\"new\":true}"));
        received.Should().ContainSingle();
        received[0].BodyInJson.Should().Be("{\"new\":true}");
        originalSend.IsCompleted.Should().BeFalse();
        pending.SetResult(response.Object);
        await originalSend;
        response.Verify(x => x.Dispose(), Times.Once);

        sender.Dispose();
        sender.UpdateExporterSettings(CreateSettings($"http://127.0.0.1:{agent.Port}").Manager.InitialExporterSettings);
        await sender.SendCompressedAsync(Compress("{\"afterDispose\":true}"));
        received.Should().ContainSingle("exporter changes must not resurrect a disposed sender");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlowTcpAgentTimesOutWithoutReplayingBatch(bool useLegacyTransport)
    {
        using var release = new ManualResetEventSlim();
        using var agent = MockTracerAgent.Create(output);
        var requests = 0;
        agent.EventPlatformProxyPayloadReceived += (_, _) =>
        {
            Interlocked.Increment(ref requests);
            release.Wait(TimeSpan.FromSeconds(20));
        };
        var agentUri = new Uri($"http://127.0.0.1:{agent.Port}");
        // Modern runtimes honor HttpWebRequest.Timeout, unlike .NET Framework's async path.
        // Leave that property unset to verify the sender independently bounds legacy requests.
        using var sender = useLegacyTransport
                               ? new FlagEvaluationAgentSender(new ApiWebRequestFactory(agentUri, FlagEvaluationAgentHeaderHelper.Instance.DefaultHeaders))
                               : new FlagEvaluationAgentSender(CreateSettings(agentUri.ToString()).Manager.InitialExporterSettings);
        var elapsed = Stopwatch.StartNew();
        var send = sender.SendCompressedAsync(Compress("{}"));
        try
        {
            (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(15)))).Should().BeSameAs(send);
            await send;
            elapsed.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(4)).And.BeLessThan(TimeSpan.FromSeconds(15));
            Volatile.Read(ref requests).Should().Be(1);
        }
        finally
        {
            release.Set();
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public async Task ConfiguredUnixSocketReceivesAgentPayload()
    {
        using var agent = MockTracerAgent.Create(output, new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null));
        var received = new List<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Add(args.Value);
        using var sender = new FlagEvaluationAgentSender(CreateSettings("unix://" + agent.TracesUdsPath).Manager.InitialExporterSettings);
        await sender.SendCompressedAsync(Compress("{}"));
        received.Should().ContainSingle();
        received[0].PathAndQuery.Should().Be("/evp_proxy/v2/api/v2/flagevaluation");
        received[0].Headers["DD-EVP-ORIGIN"].Should().Be("dd-trace-dotnet");
        received[0].Headers["DD-EVP-ORIGIN-VERSION"].Should().Be(TracerConstants.ThreePartVersion);
        received[0].BodyInJson.Should().Be("{}");
    }
#endif

    [SkippableFact]
    [Trait("Category", "LinuxUnsupported")]
    public async Task ConfiguredNamedPipeReceivesAgentPayload()
    {
        SkipOn.AllExcept(SkipOn.PlatformValue.Windows);
        using var agent = MockTracerAgent.Create(output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", null));
        var received = new List<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Add(args.Value);
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection
        {
            { "DD_TRACE_PIPE_NAME", agent.TracesWindowsPipeName },
        }));
        using var sender = new FlagEvaluationAgentSender(settings.Manager.InitialExporterSettings);
        await sender.SendCompressedAsync(Compress("{}"));
        received.Should().ContainSingle();
        received[0].PathAndQuery.Should().Be("/evp_proxy/v2/api/v2/flagevaluation");
        received[0].Headers["DD-EVP-ORIGIN"].Should().Be("dd-trace-dotnet");
        received[0].Headers["DD-EVP-ORIGIN-VERSION"].Should().Be(TracerConstants.ThreePartVersion);
        received[0].BodyInJson.Should().Be("{}");
    }

    private static TracerSettings CreateSettings(string agentUrl, string source = "remote_config") => new(new NameValueConfigurationSource(new NameValueCollection
    {
        { "DD_TRACE_AGENT_URL", agentUrl },
        { "DD_API_KEY", "configuration-credential-canary" },
        { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, source },
    }));

    private static ArraySegment<byte> Compress(string json)
    {
        using var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return new ArraySegment<byte>(stream.ToArray());
    }
}
