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
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
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
        public async Task TestCreatePlaceholderScopeWhenTraceDisabled()
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.TraceEnabled, false } });
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);

            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" } }.Wrap();
            LambdaCommon.CreatePlaceholderScope(tracer, headers).Should().BeNull();
        }

        [Fact]
        public async Task TestSendStartInvocationWhenTraceDisabled()
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.TraceEnabled, false } });
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var previous = Tracer.Instance;
            Tracer.UnsafeSetTracerInstance(tracer);
            try
            {
                LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}", null).Should().BeNull();
                _lambdaRequestMock.Verify(lr => lr.GetStartInvocationRequest(), Times.Never);
            }
            finally
            {
                Tracer.UnsafeSetTracerInstance(previous);
            }
        }

        [Fact]
        public async Task TestEndInvocationAsyncWhenTraceDisabled()
        {
            var settings = TracerSettings.Create(new() { { ConfigurationKeys.TraceEnabled, false } });
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var previous = Tracer.Instance;
            Tracer.UnsafeSetTracerInstance(tracer);
            try
            {
                var stateObject = new CallTargetState(scope: null, state: "example-aws-request-id");
                await LambdaCommon.EndInvocationAsync("{}", exception: null, stateObject, _lambdaRequestMock.Object);
                _lambdaRequestMock.Verify(lr => lr.GetEndInvocationRequest(It.IsAny<CallTargetState>(), It.IsAny<bool>()), Times.Never);
            }
            finally
            {
                Tracer.UnsafeSetTracerInstance(previous);
            }
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestEndInvocationAsyncFinishesScopeWhenTraceDisabledAfterStart()
        {
            await using var enabledTracer = TracerHelper.CreateWithFakeAgent();
            var disabledSettings = TracerSettings.Create(new() { { ConfigurationKeys.TraceEnabled, false } });
            await using var disabledTracer = TracerHelper.CreateWithFakeAgent(disabledSettings);

            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(enabledTracer, headers);
            scope.Should().NotBeNull();

            var exception = new InvalidOperationException("boom");
            var stateObject = new CallTargetState(scope, "example-aws-request-id");
            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);
            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(stateObject, true)).Returns(httpRequest.Object);

            var previous = Tracer.Instance;
            Tracer.UnsafeSetTracerInstance(disabledTracer);
            try
            {
                await LambdaCommon.EndInvocationAsync("{}", exception, stateObject, _lambdaRequestMock.Object);
                _lambdaRequestMock.Verify(lr => lr.GetEndInvocationRequest(stateObject, true), Times.Once);
                scope.Span.IsFinished.Should().BeTrue();
                scope.Span.Error.Should().BeTrue();
            }
            finally
            {
                Tracer.UnsafeSetTracerInstance(previous);
            }
        }

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

            Assert.Throws<WebException>(() => LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}", null));
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

            LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}", null).Should().BeNull();
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

            LambdaCommon.SendStartInvocation(_lambdaRequestMock.Object, "{}", null).Should().NotBeNull();
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSendEndInvocationFailure()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" }, { HttpHeaderNames.SamplingPriority, "-1" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope, state);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Throws(new WebException());
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(stateObject, true)).Returns(httpRequest.Object);

            Assert.Throws<WebException>(() => LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, stateObject, true, "{}"));
        }

        [Fact]
        [Trait("Category", "ArmUnsupported")]
        public async Task TestSendEndInvocationSuccess()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" }, { HttpHeaderNames.SamplingPriority, "-1" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope, state);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.OK);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(stateObject, true)).Returns(httpRequest.Object);
            var output = new StringWriter();
            Console.SetOut(output);
            LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, stateObject, true, "{}");
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
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope, state);

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            var responseStream = new Mock<Stream>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(HttpStatusCode.BadGateway);

            var httpRequest = new Mock<WebRequest>();
            httpRequest.Setup(h => h.GetResponse()).Returns(response.Object);
            httpRequest.Setup(h => h.GetRequestStream()).Returns(responseStream.Object);

            _lambdaRequestMock.Setup(lr => lr.GetEndInvocationRequest(stateObject, true)).Returns(httpRequest.Object);
            var output = new StringWriter();
            Console.SetOut(output);
            LambdaCommon.SendEndInvocation(_lambdaRequestMock.Object, stateObject, true, "{}");
            httpRequest.Verify(r => r.GetResponse(), Times.Once);
            Assert.Contains("Extension does not send a status 200 OK", output.ToString());
        }
    }
}
#endif
