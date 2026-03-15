// <copyright file="TestDuckBasePrivateFieldTargetBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckBasePrivateFieldTargetBase
{
    private int _hidden;

    protected TestDuckBasePrivateFieldTargetBase(int hidden)
    {
        _hidden = hidden;
    }

    internal int ReadHidden()
    {
        return _hidden;
    }
}
