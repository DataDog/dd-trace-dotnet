// <copyright file="ServerlessCompatIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Serverless;
using Datadog.Trace.ClrProfiler.CallTarget;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Serverless;

public class ServerlessCompatIntegrationTests
{
    // Exception-path guard: OnMethodEnd short-circuits before reaching Tracer.Instance, so this
    // unit test is safe. The non-exception path is covered by integration tests elsewhere —
    // Tracer.Instance global state is unsafe to rely on in unit tests (per Andrew's review).
    [Theory]
    [InlineData("trace")]
    [InlineData("dogstatsd")]
    public void OnMethodEnd_WithException_PassesThroughOriginalValue(string pipeType)
    {
        const string originalValue = "original_pipe_name";
        var exception = new InvalidOperationException("Test exception");

        CallTargetReturn<string> result = pipeType == "trace"
            ? CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(null!, originalValue, exception, default)
            : CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(null!, originalValue, exception, default);

        result.GetReturnValue().Should().Be(originalValue);
    }
}
#endif
