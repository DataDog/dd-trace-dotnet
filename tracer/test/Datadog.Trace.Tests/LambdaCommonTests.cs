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
using Datadog.Trace.TestHelpers;

using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class LambdaCommonTests
    {
        private readonly Mock<ILambdaExtensionRequest> _lambdaRequestMock = new();

        [Fact]
        public async Task TestCreatePlaceholderScopeSuccessWithTraceIdOnly()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", null);

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().Be((TraceId)1234);
            ((ISpan)scope.Span).TraceId.Should().Be(1234);
            scope.Span.SpanId.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task TestCreatePlaceholderScopeSuccessWithSamplingPriorityOnly()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, null, "-1");

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().BeGreaterThan(TraceId.Zero);
            ((ISpan)scope.Span).TraceId.Should().BeGreaterThan(0);
            scope.Span.SpanId.Should().BeGreaterThan(0);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(-1);
        }

        [Fact]
        public async Task TestCreatePlaceholderScopeSuccessWithFullContext()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().Be((TraceId)1234);
            ((ISpan)scope.Span).TraceId.Should().Be(1234);
            scope.Span.SpanId.Should().BeGreaterThan(0);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(-1);
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestCreatePlaceholderScopeSuccessWithoutContext()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, null, null);

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().BeGreaterThan((TraceId.Zero));
            ((ISpan)scope.Span).TraceId.Should().BeGreaterThan(0);
            scope.Span.SpanId.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task TestCreatePlaceholderScopeInvalidTraceId()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            Assert.Throws<FormatException>(() => LambdaCommon.CreatePlaceholderScope(tracer, "invalid-trace-id", "-1"));
        }

        [Fact]
        public async Task TestCreatePlaceholderScopeInvalidSamplingPriority()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            Assert.Throws<FormatException>(() => LambdaCommon.CreatePlaceholderScope(tracer, "1234", "invalid-sampling-priority"));
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
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");

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
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");

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
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");

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
