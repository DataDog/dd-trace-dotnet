// <copyright file="TestOptimization.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Configuration;

namespace Datadog.Trace.Ci;

internal class TestOptimization
{
    public static TestOptimization Instance { get; } = new();

    public bool Enabled => false;

    public bool IsRunning => false;

    public TestOptimizationSettings Settings { get; } = new();

    public void Initialize()
    {
    }
}
