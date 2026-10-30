// <copyright file="ProbeDataTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class ProbeDataTests
{
    [Theory]
    [CombinatorialData]
    public void IsEmpty_WhenTheEntryHasNoProcessorOrNoSampler(bool hasSampler, bool hasProcessor)
    {
        var probe = new LogProbe { Id = "probe-id", Language = "dotnet", Where = new Where { TypeName = "Samples.Target", MethodName = "Method" } };
        var probeData = new ProbeData(
            probe.Id,
            hasSampler ? new NopAdaptiveSampler() : null,
            hasProcessor ? new ProbeProcessor(probe, maxEvaluationTimeInMilliseconds: 100) : null);

        probeData.IsEmpty().Should().Be(!hasSampler || !hasProcessor);
    }

    [Fact]
    public void IsEmpty_ForEmptyAndClearedEntries()
    {
        ProbeData.Empty.IsEmpty().Should().BeTrue();
        default(ProbeData).IsEmpty().Should().BeTrue("RemoveProbes clears an entry to its default value");
    }
}
