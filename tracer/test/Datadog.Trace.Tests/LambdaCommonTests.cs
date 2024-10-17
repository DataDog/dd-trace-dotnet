// <copyright file="LambdaCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;

using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    extern alias DatadogTraceManual;

    public class LambdaCommonTests
    {
        private readonly Mock<ILambdaExtensionRequest> _lambdaRequestMock = new();

        [Fact]
        public async Task TestCreatePlaceholderScopeSuccessWithTraceIdOnly()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();

            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().Be((TraceId)1234);
            ((ISpan)scope.Span).TraceId.Should().Be(1234);
            scope.Span.SpanId.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task TestCreatePlaceholderScopeSuccessWith64BitTraceIdContext()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" }, { HttpHeaderNames.SamplingPriority, "-1" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().Be((TraceId)1234);
            ((ISpan)scope.Span).TraceId.Should().Be(1234);
            scope.Span.SpanId.Should().BeGreaterThan(0);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(-1);
        }

        [Fact]
        public async Task TestCreatePlaceholderScopeSuccessWith128BitTraceIdContext()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "5744042798732701615" }, { HttpHeaderNames.SamplingPriority, "-1" }, { HttpHeaderNames.PropagatedTags, "_dd.p.tid=1914fe7789eb32be" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);

            scope.Should().NotBeNull();
            scope.Span.TraceId128.ToString().Should().Be("1914fe7789eb32be4fb6f07e011a6faf");
            scope.Span.SpanId.Should().BeGreaterThan(0);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(-1);
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestCreatePlaceholderScopeSuccessWithoutContext()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, new WebHeaderCollection().Wrap());

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().BeGreaterThan((TraceId.Zero));
            ((ISpan)scope.Span).TraceId.Should().BeGreaterThan(0);
            scope.Span.SpanId.Should().BeGreaterThan(0);
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public void TestSendStartInvocationThrow()
        {
            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Throws(new WebException());
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetStartInvocationRequest()).Returns(httpRequest.Object);

            Assert.Throws<WebException>(() => LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}", new Dictionary<string, string>()));
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public void TestSendStartInvocationNull()
        {
            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);
            response.Setup(c => c.Headers.Get("x-datadog-trace-id")).Returns("1111");
            response.Setup(c => c.Headers.Get("x-datadog-sampling-priority")).Returns("-1");

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetStartInvocationRequest()).Returns(httpRequest.Object);

            LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}", new Dictionary<string, string>()).Should().BeNull();
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public void TestSendStartInvocationSuccess()
        {
            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            response.Setup(c => c.Headers.Get("x-datadog-trace-id")).Returns("1111");
            response.Setup(c => c.Headers.Get("x-datadog-sampling-priority")).Returns("-1");

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetStartInvocationRequest()).Returns(httpRequest.Object);

            LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}", new Dictionary<string, string>()).Should().NotBeNull();
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSendEndInvocationFailure()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" }, { HttpHeaderNames.SamplingPriority, "-1" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Throws(new WebException());
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(scope, true)).Returns(httpRequest.Object);

            Assert.Throws<WebException>(() => LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, scope, true, "{}"));
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSendEndInvocationSuccess()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" }, { HttpHeaderNames.SamplingPriority, "-1" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(scope, true)).Returns(httpRequest.Object);
            var output = new StringWriter();
            Console.SetOut(output);
            LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, scope, true, "{}");
            httpRequest.Verify(r => r.GetResponse(), Times.Once);
            Assert.Empty(output.ToString());
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSendEndInvocationFalse()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" }, { HttpHeaderNames.SamplingPriority, "-1" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(scope, true)).Returns(httpRequest.Object);
            var output = new StringWriter();
            Console.SetOut(output);
            LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, scope, true, "{}");
            httpRequest.Verify(r => r.GetResponse(), Times.Once);
            Assert.Contains("Extension does not send a status 200 OK", output.ToString());
        }
    }
}
#endif
