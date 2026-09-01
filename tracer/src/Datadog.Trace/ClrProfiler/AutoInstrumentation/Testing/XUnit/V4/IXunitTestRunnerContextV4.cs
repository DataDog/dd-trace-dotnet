// <copyright file="IXunitTestRunnerContextV4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V4;

internal interface IXunitTestRunnerContextV4 : IContextBaseV3
{
    MethodInfo Method { get; }

    object?[] MethodArguments { get; }

    IXunitTestV4 Test { get; }
}
