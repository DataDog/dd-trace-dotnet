// <copyright file="TestDuckByRefReverseConversionTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckByRefReverseConversionTarget
{
    [DuckReverseMethod]
    private bool TryGetInner(out ITestDuckByRefReverseConversionInnerProxy value)
    {
        value = DuckType.Create<ITestDuckByRefReverseConversionInnerProxy>(new TestDuckByRefReverseConversionInnerTarget("from-out"));
        return true;
    }

    [DuckReverseMethod]
    private bool RoundtripInner(ref ITestDuckByRefReverseConversionInnerProxy value)
    {
        var currentName = value?.Name ?? "null";
        value = DuckType.Create<ITestDuckByRefReverseConversionInnerProxy>(new TestDuckByRefReverseConversionInnerTarget($"{currentName}-roundtrip"));
        return true;
    }

    [DuckReverseMethod]
    private void Increment(ref object value)
    {
        value = (int)value + 1;
    }

    [DuckReverseMethod]
    private void GetNumber(out object value)
    {
        value = 42;
    }
}
