// <copyright file="LambdaCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        private readonly Mock<ILambdaRequest> _lambdaRequestMock = new Mock<ILambdaRequest>();

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestCreatePlaceholderScopeSuccess()
        {
            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            response.Setup(c => c.Headers.Get("x-datadog-trace-id")).Returns("1111");
            response.Setup(c => c.Headers.Get("x-datadog-span-id")).Returns("2222");

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);

            _lambdaRequestMock.Setup(lr => lr.GetTraceContextRequest()).Returns(httpRequest.Object);

            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, _lambdaRequestMock.Object);

            scope.Should().NotBeNull();
            scope.Span.TraceId.ToString().Should().Be("1111");
            scope.Span.SpanId.ToString().Should().Be("2222");
        }

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestCreatePlaceholderScopeFailureDueToInvalidLong()
        {
            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            response.Setup(c => c.Headers.Get("x-datadog-trace-id")).Returns("#$#");
            response.Setup(c => c.Headers.Get("x-datadog-span-id")).Returns("xxx_yyy");

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);

            _lambdaRequestMock.Setup(lr => lr.GetTraceContextRequest()).Returns(httpRequest.Object);

            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, _lambdaRequestMock.Object);

            scope.Should().BeNull();
        }

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestCreatePlaceholderScopeFailureDueToHttpError()
        {
            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Throws(new WebException());

            _lambdaRequestMock.Setup(lr => lr.GetTraceContextRequest()).Returns(httpRequest.Object);

            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, _lambdaRequestMock.Object);

            scope.Should().BeNull();
        }

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestSendStartInvocationFailure()
        {
            var contextResponse = new Mock<HttpWebResponse>(MockBehavior.Loose);
            contextResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            contextResponse.Setup(c => c.Headers.Get("x-datadog-trace-id")).Returns("1111");
            contextResponse.Setup(c => c.Headers.Get("x-datadog-span-id")).Returns("2222");

            var httpContextRequest = new Mock<WebRequest>();
            httpContextRequest.Setup(h => h.GetResponse()).Returns(contextResponse.Object);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Throws(new WebException());
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetTraceContextRequest()).Returns(httpContextRequest.Object);
            _lambdaRequestMock.Setup(lr => lr.GetStartInvocationRequest()).Returns(httpRequest.Object);

            Assert.Throws<WebException>(() => LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}"));
        }

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestSendStartInvocationTrue()
        {
            var contextResponse = new Mock<HttpWebResponse>(MockBehavior.Loose);
            contextResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            contextResponse.Setup(c => c.Headers.Get("x-datadog-trace-id")).Returns("1111");
            contextResponse.Setup(c => c.Headers.Get("x-datadog-span-id")).Returns("2222");

            var httpContextRequest = new Mock<WebRequest>();
            httpContextRequest.Setup(h => h.GetResponse()).Returns(contextResponse.Object);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetTraceContextRequest()).Returns(httpContextRequest.Object);
            _lambdaRequestMock.Setup(lr => lr.GetStartInvocationRequest()).Returns(httpRequest.Object);

            LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}").Should().Be(true);
        }

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestSendStartInvocationFalse()
        {
            var contextResponse = new Mock<HttpWebResponse>(MockBehavior.Loose);
            contextResponse.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);
            contextResponse.Setup(c => c.Headers.Get("x-datadog-trace-id")).Returns("1111");
            contextResponse.Setup(c => c.Headers.Get("x-datadog-span-id")).Returns("2222");

            var httpContextRequest = new Mock<WebRequest>();
            httpContextRequest.Setup(h => h.GetResponse()).Returns(contextResponse.Object);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetTraceContextRequest()).Returns(httpContextRequest.Object);
            _lambdaRequestMock.Setup(lr => lr.GetStartInvocationRequest()).Returns(httpRequest.Object);

            LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}").Should().Be(false);
        }

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestSendEndInvocationFailure()
        {
            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Throws(new WebException());

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(true)).Returns(httpRequest.Object);

            Assert.Throws<WebException>(() => LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, true));
        }

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestSendEndInvocationTrue()
        {
            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(true)).Returns(httpRequest.Object);

            LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, true).Should().Be(true);
        }

        [Fact]
        [Trait("Category", "Lambda")]
        public void TestSendEndInvocationFalse()
        {
            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(true)).Returns(httpRequest.Object);

            LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, true).Should().Be(false);
        }
    }
}
