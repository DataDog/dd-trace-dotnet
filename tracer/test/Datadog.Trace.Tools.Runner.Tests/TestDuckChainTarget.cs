// <copyright file="TestDuckChainTarget.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckChainTarget
{
    private TestDuckChainInnerTarget _lastReceived;

    public TestDuckChainInnerTarget LastReceived => _lastReceived;

    public TestDuckChainInnerTarget Roundtrip(TestDuckChainInnerTarget value)
    {
        _lastReceived = value;
        return value;
    }

    public TestDuckChainInnerTarget Create(string name)
    {
        return new TestDuckChainInnerTarget(name);
    }

    public TestDuckChainInnerTarget CreateNull()
    {
        return null;
    }
}
