// <copyright file="StubOnAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Benchmarks.Trace;

/// <summary>
/// Marks a benchmark class whose body compiles to a stub (see
/// AutoInstrumentation/AspNetCoreBenchmark.cs) on the given target frameworks.
/// The stub measures harness overhead instead of a scenario, so the filter in
/// Program.cs skips the class on those target frameworks.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class StubOnAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StubOnAttribute"/> class.
    /// </summary>
    /// <param name="targetFrameworks">The target frameworks from Benchmarks.Trace.csproj, e.g. "net472".</param>
    public StubOnAttribute(params string[] targetFrameworks) => TargetFrameworks = targetFrameworks;

    /// <summary>
    /// Gets the target frameworks on which the benchmark body is a stub.
    /// </summary>
    public string[] TargetFrameworks { get; }
}
