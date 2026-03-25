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

    [Theory]
    [InlineData("trace", "dd_trace_from_compat_layer")]
    [InlineData("dogstatsd", "dd_dogstatsd_from_compat_layer")]
    public void OnMethodEnd_WhenTracerHasNoPipeName_FallsBackToReturnValue(string pipeType, string compatLayerName)
    {
        // In a unit test environment, Tracer.Instance won't have pipe names configured,
        // so the integration should fall back to the compat layer's own calculated name.
        CallTargetReturn<string> result = pipeType == "trace"
            ? CompatibilityLayer_CalculateTracePipeName_Integration.OnMethodEnd<object>(null!, compatLayerName, null!, default)
            : CompatibilityLayer_CalculateDogStatsDPipeName_Integration.OnMethodEnd<object>(null!, compatLayerName, null!, default);

        result.GetReturnValue().Should().Be(compatLayerName);
    }
}
#endif
