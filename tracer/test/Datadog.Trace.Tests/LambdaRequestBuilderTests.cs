// <copyright file="LambdaRequestBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;

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

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope, isError: true);
            request.Headers.Get("x-datadog-invocation-error").Should().Be("true");
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithoutError()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection().Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope, isError: false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithScope()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection { { HttpHeaderNames.TraceId, "1234" }, { HttpHeaderNames.SamplingPriority, "-1" } }.Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope, isError: false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("-1");
            request.Headers.Get("x-datadog-trace-id").Should().Be("1234");
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
        }

        [Fact]
        public void TestGetEndInvocationRequestWithoutScope()
        {
            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope: null, isError: false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().BeNull();
            request.Headers.Get("x-datadog-trace-id").Should().BeNull();
            request.Headers.Get("x-datadog-span-id").Should().BeNull();
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithErrorTags()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection().Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);
            scope.Span.SetTag("error.msg", "Exception");
            scope.Span.SetTag("error.type", "Exception");
            scope.Span.SetTag("error.stack", "everything is " + System.Environment.NewLine + "fine");

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope, true);
            request.Headers.Get("x-datadog-invocation-error").Should().NotBeNull();
            request.Headers.Get("x-datadog-invocation-error-msg").Should().Be("Exception");
            request.Headers.Get("x-datadog-invocation-error-type").Should().Be("Exception");
            request.Headers.Get("x-datadog-invocation-error-stack").Should().NotBeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
        }

        [Fact]
        public async Task TestGetEndInvocationRequestWithoutErrorTags()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var headers = new WebHeaderCollection().Wrap();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, headers);

            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope, true);
            request.Headers.Get("x-datadog-invocation-error").Should().NotBeNull();
            request.Headers.Get("x-datadog-invocation-error-msg").Should().BeNull();
            request.Headers.Get("x-datadog-invocation-error-type").Should().BeNull();
            request.Headers.Get("x-datadog-invocation-error-stack").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
        }
    }
}
#endif
