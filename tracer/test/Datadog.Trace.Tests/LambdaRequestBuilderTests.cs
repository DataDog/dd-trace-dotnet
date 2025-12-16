// <copyright file="LambdaRequestBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER
using System;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;
using LambdaCommon = Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda.LambdaCommon;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(WebRequestCollection))]
    public class LambdaRequestBuilderTests
    {
        [Fact]
        public async Task TestGetEndInvocationRequestWithError()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection().Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope, state);

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(stateObject, true);
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
            request.Headers.Get("lambda-runtime-aws-request-id").Should().Be("example-aws-request-id");
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithoutError()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection().Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope, state);

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(stateObject, isError: false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
            request.Headers.Get("lambda-runtime-aws-request-id").Should().Be("example-aws-request-id");
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithScope()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope, state);

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(stateObject, isError: false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().Be("1234");
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
            request.Headers.Get("lambda-runtime-aws-request-id").Should().Be("example-aws-request-id");
        }

        [Fact]
        public void TestGetEndInvocationRequestWithoutScope()
        {
            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope: null, state);

            var request = requestBuilder.GetEndInvocationRequest(stateObject, isError: false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().BeNull();
            request.Headers.Get("x-datadog-trace-id").Should().BeNull();
            request.Headers.Get("x-datadog-span-id").Should().BeNull();
            request.Headers.Get("lambda-runtime-aws-request-id").Should().Be("example-aws-request-id");
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithoutState()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            var stateObject = new CallTargetState(scope, state: null);

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(stateObject, isError: false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().Be("1234");
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
            request.Headers.Get("lambda-runtime-aws-request-id").Should().BeNull();
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithErrorTags()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection().Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope, state);

            var errorMsg = "Exception";
            var errorType = "Exception";
            var errorStack = "everything is " + System.Environment.NewLine + "fine";
            scope.Span.SetTag("error.msg", errorMsg);
            scope.Span.SetTag("error.type", errorType);
            scope.Span.SetTag("error.stack", errorStack);

            var expectedErrorMsg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(errorMsg));
            var expectedErrorType = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(errorType));
            var expectedErrorStack = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(errorStack));

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(stateObject, true);
            request.Headers.Get("x-datadog-invocation-error").Should().NotBeNull();
            request.Headers.Get("x-datadog-invocation-error-msg").Should().Be(expectedErrorMsg);
            request.Headers.Get("x-datadog-invocation-error-type").Should().Be(expectedErrorType);
            request.Headers.Get("x-datadog-invocation-error-stack").Should().Be(expectedErrorStack);
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
            request.Headers.Get("lambda-runtime-aws-request-id").Should().Be("example-aws-request-id");
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithoutErrorTags()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection().Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            var state = "example-aws-request-id";
            var stateObject = new CallTargetState(scope, state);

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(stateObject, true);
            request.Headers.Get("x-datadog-invocation-error").Should().NotBeNull();
            request.Headers.Get("x-datadog-invocation-error-msg").Should().BeNull();
            request.Headers.Get("x-datadog-invocation-error-type").Should().BeNull();
            request.Headers.Get("x-datadog-invocation-error-stack").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
            request.Headers.Get("lambda-runtime-aws-request-id").Should().Be("example-aws-request-id");
        }
    }
}
#endif
