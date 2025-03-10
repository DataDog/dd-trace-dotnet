// <copyright file="IXunitTestMethodRunnerBaseContextV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Context class for XunitTestMethodRunnerBase.
/// </summary>
internal interface IXunitTestMethodRunnerBaseContextV3 : IContextBaseV3
{
    /// <summary>
    /// Gets the arguments to send to the test class constructor.
    /// </summary>
    object?[] ConstructorArguments { get; }

    /// <summary>
    /// Gets the test method that is being executed.
    /// </summary>
    IXunitTestMethodV3 TestMethod { get; }
}
