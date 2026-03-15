// <copyright file="TestDuckStructTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal struct TestDuckStructTarget
{
    private readonly int _seed;

    internal TestDuckStructTarget(int seed)
    {
        _seed = seed;
    }

    private int Add(int value)
    {
        return _seed + value;
    }
}
