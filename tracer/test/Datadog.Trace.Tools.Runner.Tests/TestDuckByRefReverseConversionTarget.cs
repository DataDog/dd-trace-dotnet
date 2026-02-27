// <copyright file="TestDuckByRefReverseConversionTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckByRefReverseConversionTarget
{
    private bool TryGetInner(out TestDuckByRefReverseConversionInnerTarget value)
    {
        value = new TestDuckByRefReverseConversionInnerTarget("from-out");
        return true;
    }

    private bool RoundtripInner(ref TestDuckByRefReverseConversionInnerTarget value)
    {
        var currentName = value?.Name ?? "null";
        value = new TestDuckByRefReverseConversionInnerTarget($"{currentName}-roundtrip");
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
