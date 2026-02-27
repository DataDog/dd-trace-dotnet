// <copyright file="TestDuckByRefConversionTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckByRefConversionTarget
{
    private bool TryGetInner(out TestDuckByRefConversionInnerTarget value)
    {
        value = new TestDuckByRefConversionInnerTarget("from-out");
        return true;
    }

    private bool RoundtripInner(ref TestDuckByRefConversionInnerTarget value)
    {
        var currentName = value?.Name ?? "null";
        value = new TestDuckByRefConversionInnerTarget($"{currentName}-roundtrip");
        return true;
    }

    private void Increment(ref int value)
    {
        value++;
    }

    private void GetNumber(out int value)
    {
        value = 42;
    }
}
