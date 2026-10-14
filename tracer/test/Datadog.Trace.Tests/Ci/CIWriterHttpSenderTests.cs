// <copyright file="CIWriterHttpSenderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Agent.MessagePack;
using Datadog.Trace.Ci.Agent.Payloads;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.EventModel;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

[Collection(nameof(CoverageGlobalStateTestCollection))]
public class CIWriterHttpSenderTests
{
    [Theory]
    [CombinatorialData]
    public async Task CompressionHeaderMatchesTheEncodedBodyWhenDiscoveryCompletes(bool discoveryComplete, bool retryFirstRequest)
    {
        var settings = new TestOptimizationSettings(
            new NameValueConfigurationSource(new NameValueCollection
            {
                { ConfigurationKeys.CIVisibility.AgentlessEnabled, "0" },
                { ConfigurationKeys.AgentUri, "http://localhost:8126" },
            }),
            NullConfigurationTelemetry.Instance);
        var proxySupport = discoveryComplete ? EventPlatformProxySupport.V4 : EventPlatformProxySupport.None;
        var management = new Mock<ITestOptimizationTracerManagement>();
        management.SetupGet(x => x.EventPlatformProxySupport).Returns(() => proxySupport);
        var optimization = new Mock<ITestOptimization>();
        optimization.SetupGet(x => x.Settings).Returns(settings);
        optimization.SetupGet(x => x.TracerManagement).Returns(management.Object);
        var previousOptimization = TestOptimization.Instance;
        TestOptimization.Instance = optimization.Object;

        try
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings.TracerSettings);
            var span = tracer.StartSpan("test");
            span.ResourceName = "gzip-regression";
            span.Type = SpanTypes.Test;
            span.Finish(TimeSpan.FromMilliseconds(1));
            var testEvent = new TestEvent(span);
            // Serialization locks the event buffer, so use a separate payload for the expected bytes.
            var expectedPayload = new CITestCyclePayload(settings, CIFormatterResolver.Instance);
            expectedPayload.TryProcessEvent(testEvent).Should().BeTrue();
            var expectedBody = expectedPayload.ToArray();
            var payload = new CITestCyclePayload(settings, CIFormatterResolver.Instance);
            payload.TryProcessEvent(testEvent).Should().BeTrue();
            var response = new Mock<IApiResponse>();
            var attempts = 0;
            response.SetupGet(x => x.StatusCode).Returns(() => retryFirstRequest && attempts == 1 ? 500 : 200);
            byte[]? receivedBody = null;
            Exception? decodingError = null;
            var request = new Mock<IApiRequest>();
            request.Setup(x => x.PostAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<string>(), It.IsAny<string>()))
                   .Callback<ArraySegment<byte>, string, string>((bytes, _, encoding) =>
                    {
                        attempts++;
                        var headers = new MockHttpRequest.MockHeaders();
                        if (encoding is not null)
                        {
                            headers.Add("Content-Encoding", encoding);
                        }

                        using var stream = new MemoryStream(bytes.Array!, bytes.Offset, bytes.Count);
                        var received = new MockHttpRequest { Headers = headers, Body = stream, ContentLength = bytes.Count };
                        try
                        {
                            // Decode the same bytes and header that the production sender handed to its transport.
                            receivedBody = received.ReadStreamBody();
                        }
                        catch (Exception error)
                        {
                            decodingError = error;
                        }
                    })
                   .ReturnsAsync(response.Object);
            var factory = new Mock<IApiRequestFactory>();
            factory.Setup(x => x.Create(It.IsAny<Uri>())).Returns(() =>
            {
                // Agent discovery can finish after serialization, including before a retry.
                proxySupport = EventPlatformProxySupport.V4;
                return request.Object;
            });

            await new CIWriterHttpSender(factory.Object).SendPayloadAsync(payload);

            decodingError.Should().BeNull();
            receivedBody.Should().Equal(expectedBody);
            attempts.Should().Be(retryFirstRequest ? 2 : 1);
        }
        finally
        {
            TestOptimization.Instance = previousOptimization;
        }
    }
}
