// <copyright file="TestDuckStructToStringTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal struct TestDuckStructToStringTarget
{
    private readonly string _value;

    internal TestDuckStructToStringTarget(string value)
    {
        _value = value;
    }

    public override string ToString()
    {
        return $"struct:{_value}";
    }

    private int EchoLength(string value)
    {
        return (_value?.Length ?? 0) + (value?.Length ?? 0);
    }
}
