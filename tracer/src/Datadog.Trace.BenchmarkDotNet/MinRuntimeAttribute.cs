// <copyright file="MinRuntimeAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using BenchmarkDotNet.Jobs;

namespace Datadog.Trace.BenchmarkDotNet;

/// <summary>
/// Marks a benchmark class whose body is real only on the given runtime and
/// newer. Below it, the body compiles to a stub (see
/// AutoInstrumentation/AspNetCoreBenchmark.cs) that measures harness overhead
/// instead of a scenario, so the <see cref="DatadogExtensions.AddMinRuntimeFilter"/>
/// extension skips it.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MinRuntimeAttribute : Attribute
{
    public MinRuntimeAttribute(RuntimeMoniker minimum) => Minimum = minimum;

    /// <summary>
    /// The oldest runtime on which the benchmark body is real.
    /// </summary>
    public RuntimeMoniker Minimum { get; }
}
