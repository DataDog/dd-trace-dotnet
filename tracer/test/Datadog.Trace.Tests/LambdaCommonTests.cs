// <copyright file="LambdaCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

using Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS;
using Datadog.Trace.TestHelpers;

using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class LambdaCommonTests
    {
        private readonly Mock<ILambdaExtensionRequest> _lambdaRequestMock = new Mock<ILambdaExtensionRequest>();

        [Fact]
        public void TestCreatePlaceholderScopeSuccessWithTraceIdOnly()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", null);
            scope.Should().NotBeNull();
            scope.Span.TraceId.ToString().Should().Be("1234");
            scope.Span.SpanId.ToString().Should().NotBeNull();
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(1);
        }

        [Fact]
        public void TestCreatePlaceholderScopeSuccessWithSamplingPriorityOnly()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, null, "-1");
            scope.Should().NotBeNull();
            scope.Span.TraceId.ToString().Should().NotBeNull();
            scope.Span.SpanId.ToString().Should().NotBeNull();
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(-1);
        }

        [Fact]
        public void TestCreatePlaceholderScopeSuccessWithFullContext()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");
            scope.Should().NotBeNull();
            scope.Span.TraceId.ToString().Should().Be("1234");
            scope.Span.SpanId.ToString().Should().NotBeNull();
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(-1);
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public void TestCreatePlaceholderScopeSuccessWithoutContext()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, null, null);
            scope.Should().NotBeNull();
            scope.Span.TraceId.ToString().Should().NotBeNull();
            scope.Span.SpanId.ToString().Should().NotBeNull();
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(1);
        }

        [Fact]
        public void TestCreatePlaceholderScopeInvalidTraceId()
        {
            var tracer = TracerHelper.Create();
            Assert.Throws<FormatException>(() => LambdaCommon.CreatePlaceholderScope(tracer, "invalid-trace-id", "-1"));
        }

        [Fact]
        public void TestCreatePlaceholderScopeInvalidSamplingPriority()
        {
            var tracer = TracerHelper.Create();
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
        public void TestSendEndInvocationFailure()
        {
            var tracer = TracerHelper.Create();
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
        public void TestSendEndInvocationTrue()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(scope, true)).Returns(httpRequest.Object);

            LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, scope, true, "{}").Should().Be(true);
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public void TestSendEndInvocationFalse()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(scope, true)).Returns(httpRequest.Object);

            LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, scope, true, "{}").Should().Be(false);
        }
    }
}
