// <copyright file="Iast.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Iast;

internal class Iast
{
    public static Iast Instance { get; } = new();

    public IastSettings Settings { get; } = new();

    public OverheadControllerStub OverheadController { get; } = new();

    public void InitAnalyzers()
    {
    }

    internal class OverheadControllerStub
    {
        public void ReleaseRequest()
        {
        }

        public bool AcquireRequest() => false;
    }
}
