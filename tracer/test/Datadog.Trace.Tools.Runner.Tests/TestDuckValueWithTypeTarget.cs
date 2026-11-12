// <copyright file="TestDuckValueWithTypeTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckValueWithTypeTarget
{
    private string _lastConsumed = string.Empty;

    public string LastConsumed => _lastConsumed;

    private string Consume(string value)
    {
        _lastConsumed = value;
        return $"consume:{value}";
    }

    private string Produce(string value)
    {
        return $"produce:{value}";
    }
}
