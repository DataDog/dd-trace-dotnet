// <copyright file="TestDuckBasePrivatePropertyTargetBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Tests;

internal class TestDuckBasePrivatePropertyTargetBase
{
    private int _hidden;
    private int _inheritedVisible;

    protected TestDuckBasePrivatePropertyTargetBase(int hidden, int inheritedVisible)
    {
        _hidden = hidden;
        _inheritedVisible = inheritedVisible;
    }

    protected int InheritedVisible
    {
        get => _inheritedVisible;
        set => _inheritedVisible = value;
    }

    private int Hidden
    {
        get => _hidden;
        set => _hidden = value;
    }

    internal int ReadHidden()
    {
        return Hidden;
    }

    internal int ReadInheritedVisible()
    {
        return InheritedVisible;
    }
}
