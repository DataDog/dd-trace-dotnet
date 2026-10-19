// <copyright file="TestDuckStaticFieldTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckStaticFieldTarget
{
    private static string _value = "initial";

    internal static string ReadValue()
    {
        return _value;
    }

    internal static void ResetValue(string value)
    {
        _value = value;
    }
}
