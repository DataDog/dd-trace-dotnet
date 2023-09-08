// <copyright file="LambdaCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS;
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
        public void TestCreatePlaceholderScopeSuccessWithTraceIdOnly()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", null);

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().Be((TraceId)1234);
            ((ISpan)scope.Span).TraceId.Should().Be(1234);
            scope.Span.SpanId.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestCreatePlaceholderScopeSuccessWithSamplingPriorityOnly()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, null, "-1");

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().BeGreaterThan(TraceId.Zero);
            ((ISpan)scope.Span).TraceId.Should().BeGreaterThan(0);
            scope.Span.SpanId.Should().BeGreaterThan(0);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(-1);
        }

        [Fact]
        public void TestCreatePlaceholderScopeSuccessWithFullContext()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().Be((TraceId)1234);
            ((ISpan)scope.Span).TraceId.Should().Be(1234);
            scope.Span.SpanId.Should().BeGreaterThan(0);
            scope.Span.Context.TraceContext.SamplingPriority.Should().Be(-1);
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public void TestCreatePlaceholderScopeSuccessWithoutContext()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, null, null);

            scope.Should().NotBeNull();
            scope.Span.TraceId128.Should().BeGreaterThan((TraceId.Zero));
            ((ISpan)scope.Span).TraceId.Should().BeGreaterThan(0);
            scope.Span.SpanId.Should().BeGreaterThan(0);
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

        [Fact]
        public void SerializeObject_WithDictionary_ParsesCorrectlyToJsonString()
        {
            DateTime date = new(2023, 1, 7);
            const string jsonString = @"{ ""general"": ""kenobi"" }";
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            Dictionary<string, object> dictionary = new()
            {
                { "date", date },
                { "hello", "there" },
                { "memoryStream", memoryStream }
            };

            const string expectedValue = @"{""date"":1673049600.0,""hello"":""there"",""memoryStream"":""eyAiZ2VuZXJhbCI6ICJrZW5vYmkiIH0=""}";
            var result = LambdaCommon.SerializeObject(dictionary);

            result.Should().Be(expectedValue);
        }

        [Fact]
        public void SerializeObject_WithMemoryStream_ParsesCorrectlyToBase64String()
        {
            const string jsonString = @"{ ""hello"": ""there"" }";
            const string expectedValue = "\"eyAiaGVsbG8iOiAidGhlcmUiIH0=\"";
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            var result = LambdaCommon.SerializeObject(memoryStream);

            result.Should().Be(expectedValue);
        }

        [Fact]
        public void SerializeObject_WithDateTime_ParsesCorrectlyToUnixEpochSeconds()
        {
            DateTime date = new(2023, 1, 7);
            const string expectedValue = "1673049600.0";
            var result = LambdaCommon.SerializeObject(date);

            result.Should().Be(expectedValue);
        }
    }
}
