// <copyright file="LambdaRequestBuilderTests.cs" company="Datadog">
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
    public class LambdaRequestBuilderTests
    {
        [Fact]
        public void TestGetEndInvocationRequestWithError()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, null, null);
            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope, true);
            request.Headers.Get("x-datadog-invocation-error").Should().Be("true");
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
        }

        [Fact]
        public void TestGetEndInvocationRequestWithoutError()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, null, null);
            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope, false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().Be("1");
            request.Headers.Get("x-datadog-trace-id").Should().NotBeNull();
            request.Headers.Get("x-datadog-span-id").Should().NotBeNull();
        }

        [Fact]
        public void TestGetEndInvocationRequestWithScope()
        {
            var tracer = TracerHelper.Create();
            var scope = LambdaCommon.CreatePlaceholderScope(tracer, "1234", "-1");
            ILambdaExtensionRequest requestBuilder = new LambdaRequestBuilder();
            var request = requestBuilder.GetEndInvocationRequest(scope, false);
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
            var request = requestBuilder.GetEndInvocationRequest(null, false);
            request.Headers.Get("x-datadog-invocation-error").Should().BeNull();
            request.Headers.Get("x-datadog-tracing-enabled").Should().Be("false");
            request.Headers.Get("x-datadog-sampling-priority").Should().BeNull();
            request.Headers.Get("x-datadog-trace-id").Should().BeNull();
            request.Headers.Get("x-datadog-span-id").Should().BeNull();
        }
    }
}
