// <copyright file="TestDuckStructCopyProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Tools.Runner.Tests;

#pragma warning disable CS0649
[DuckCopy]
internal struct TestDuckStructCopyProxy
{
    public string Name;

    public int Count;
}
#pragma warning restore CS0649
