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
using Datadog.Trace.Agent;
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

    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class LambdaCommonTests
    {
        private readonly Mock<ILambdaExtensionRequest> _lambdaRequestMock = new();

        [Theory]
        [InlineData(ConfigurationKeys.TraceEnabled)]
        [InlineData("DD_TRACE_AWSLAMBDA_ENABLED")]
        public async Task TestOnDelegateBeginSkipsStartInvocationWhenIntegrationDisabled(string disabledKey)
        {
            var settings = TracerSettings.Create(new() { { disabledKey, false } });
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            // Without the enabled check this would call start-invocation on the (absent) extension and throw
            var arg = new object();
            new HandlerWrapperSetHandlerIntegration.Async1Callbacks().OnDelegateBegin(sender: null, ref arg).Should().BeNull();
        }

        [Theory]
        [InlineData(null)] // enabled now, but disabled when the invocation started
        [InlineData(ConfigurationKeys.TraceEnabled)]
        [InlineData("DD_TRACE_AWSLAMBDA_ENABLED")]
        public async Task TestOnDelegateEndAsyncFlushesWithoutEndInvocationWhenStartWasSkipped(string disabledKey)
        {
            var settings = disabledKey is null ? new TracerSettings() : TracerSettings.Create(new() { { disabledKey, false } });
            var agentWriter = new Mock<IAgentWriter>();
            agentWriter.Setup(w => w.FlushTracesAsync()).Returns(Task.CompletedTask);
            await using var tracer = TracerHelper.Create(settings, agentWriter.Object);
            Tracer.UnsafeSetTracerInstance(tracer);

            var originalOut = Console.Out;
            var output = new StringWriter();
            Console.SetOut(output);
            string result;
            try
            {
                // state is null when OnDelegateBegin skipped the invocation
                result = await new HandlerWrapperSetHandlerIntegration.Async1Callbacks().OnDelegateEndAsync(sender: null, "response", exception: null, state: null);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            result.Should().Be("response");
            agentWriter.Verify(w => w.FlushTracesAsync(), Times.Once);
            // The end-invocation path logs (and fails, with no extension); the skipped path writes nothing
            output.ToString().Should().BeEmpty();
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

            Tracer.UnsafeSetTracerInstance(disabledTracer);

            await LambdaCommon.EndInvocationAsync("{}", exception, stateObject, _lambdaRequestMock.Object);
            _lambdaRequestMock.Verify(lr => lr.GetEndInvocationRequest(stateObject, true), Times.Once);
            scope.Span.IsFinished.Should().BeTrue();
            scope.Span.Error.Should().BeTrue();
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
