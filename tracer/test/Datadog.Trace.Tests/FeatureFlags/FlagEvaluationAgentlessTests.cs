// <copyright file="FlagEvaluationAgentlessTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FlagEvaluationAgentlessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectDeliveryPreservesWriterCompressionAndPrivacy(bool consent)
    {
        var local = new Mock<IApiRequestFactory>(MockBehavior.Strict);
        var direct = new Mock<IApiRequestFactory>(MockBehavior.Strict);
        var request = new Mock<IApiRequest>(MockBehavior.Strict);
        var response = new Mock<IApiResponse>();
        response.SetupGet(x => x.StatusCode).Returns(202);
        var endpoint = new Uri("https://event-platform-intake.datadoghq.com/api/v2/flagevaluation");
        direct.Setup(x => x.GetEndpoint(FeatureFlagsEvpTransport.FlagEvaluationIntakePath)).Returns(endpoint);
        direct.Setup(x => x.Create(endpoint)).Returns(request.Object);
        byte[]? captured = null;
        request.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", "gzip"))
               .Callback<ArraySegment<byte>, string, string>((bytes, _, _) =>
               {
                   captured = new byte[bytes.Count];
                   Array.Copy(bytes.Array!, bytes.Offset, captured, 0, bytes.Count);
               })
               .ReturnsAsync(response.Object);
        using var transport = new FeatureFlagsEvpTransport(FeatureFlagsSource.Agentless, local.Object, direct.Object, NullDiscoveryService.Instance);
        var writer = new FlagEvaluationWriter(transport.SendFlagEvaluationsAsync, new Dictionary<string, string> { ["service"] = "direct-test" });
        try
        {
            var observation = new FlagEvalEvent(
                "flag",
                "variant",
                null,
                "subject-canary",
                1790000000000,
                new Dictionary<string, object?> { ["field"] = "context-canary" },
                observeFullEvaluationData: consent);
            writer.TryEnqueue(observation).Should().BeTrue();
            await writer.FlushAsync();
            captured.Should().NotBeNull();
            using var input = new MemoryStream(captured!);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            if (consent)
            {
                json.Should().Contain("subject-canary").And.Contain("context-canary");
            }
            else
            {
                json.Should().NotContain("subject-canary").And.NotContain("context-canary").And.Contain("sha256_");
            }

            var payload = JObject.Parse(json);
            payload["flagEvaluations"]![0]!["evaluation_count"]!.Value<int>().Should().Be(1);
            payload["context"]!["service"]!.Value<string>().Should().Be("direct-test");
            await writer.FlushAsync();
            request.Verify(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", "gzip"), Times.Once);
            local.VerifyNoOtherCalls();
        }
        finally
        {
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Theory]
    [InlineData(404, true)]
    [InlineData(403, false)]
    public async Task CompressedBatchUsesExistingReplayAndStickyFallbackPolicy(int status, bool replay)
    {
        var local = new Mock<IApiRequestFactory>();
        var direct = new Mock<IApiRequestFactory>();
        var localRequest = new Mock<IApiRequest>();
        var directRequest = new Mock<IApiRequest>();
        var rejected = new Mock<IApiResponse>();
        var accepted = new Mock<IApiResponse>();
        rejected.SetupGet(x => x.StatusCode).Returns(status);
        accepted.SetupGet(x => x.StatusCode).Returns(202);
        local.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns(new Uri("http://agent:8126/evp_proxy/v4/api/v2/flagevaluation"));
        direct.Setup(x => x.GetEndpoint(It.IsAny<string>())).Returns(new Uri("https://event-platform-intake.datadoghq.com/api/v2/flagevaluation"));
        local.Setup(x => x.Create(It.IsAny<Uri>())).Returns(localRequest.Object);
        direct.Setup(x => x.Create(It.IsAny<Uri>())).Returns(directRequest.Object);
        localRequest.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", "gzip")).ReturnsAsync(rejected.Object);
        directRequest.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), "application/json", "gzip")).ReturnsAsync(accepted.Object);
        using var transport = new FeatureFlagsEvpTransport(FeatureFlagsSource.Agentless, local.Object, direct.Object, NullDiscoveryService.Instance, initialLocalProxyEndpoint: "evp_proxy/v4");
        var first = new ArraySegment<byte>(new byte[] { 1 });
        var second = new ArraySegment<byte>(new byte[] { 2 });

        await transport.SendFlagEvaluationsAsync(first);
        await transport.SendFlagEvaluationsAsync(second);

        localRequest.Verify(x => x.PostAsync(first, "application/json", "gzip"), Times.Once);
        localRequest.Verify(x => x.PostAsync(second, "application/json", "gzip"), Times.Never);
        directRequest.Verify(x => x.PostAsync(first, "application/json", "gzip"), replay ? Times.Once() : Times.Never());
        directRequest.Verify(x => x.PostAsync(second, "application/json", "gzip"), Times.Once);
    }

    [Fact]
    public async Task ShutdownDuringDiscoveryDoesNotStartLateDirectDelivery()
    {
        var local = new Mock<IApiRequestFactory>(MockBehavior.Strict);
        var direct = new Mock<IApiRequestFactory>(MockBehavior.Strict);
        using var transport = new FeatureFlagsEvpTransport(
            FeatureFlagsSource.Agentless,
            local.Object,
            direct.Object,
            new FlagEvaluationTestDiscovery(),
            initialDiscoveryKnown: false,
            initialDiscoveryWait: TimeSpan.FromMinutes(1));
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer = new FlagEvaluationWriter(
            async bytes =>
            {
                started.TrySetResult(true);
                try
                {
                    await transport.SendFlagEvaluationsAsync(bytes);
                }
                finally
                {
                    completed.TrySetResult(true);
                }
            },
            new Dictionary<string, string> { ["service"] = "shutdown-test" });
        try
        {
            writer.TryEnqueue(new FlagEvalEvent("flag", "variant", null, "user", 1790000000000, null)).Should().BeTrue();
            var flush = writer.FlushAsync();
            (await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(started.Task);
            var close = writer.CloseAsync(TimeSpan.FromMilliseconds(50));
            (await Task.WhenAny(close, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(close);
            await close;
            transport.Dispose();
            (await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(completed.Task);
            await flush;
            local.VerifyNoOtherCalls();
            direct.VerifyNoOtherCalls();
        }
        finally
        {
            transport.Dispose();
            await writer.CloseAsync(TimeSpan.FromSeconds(2));
        }
    }
}
