// <copyright file="IXunitTestClassRunnerContextV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// XunitTestClassRunnerContext proxy
/// </summary>
internal interface IXunitTestClassRunnerContextV3
{
    /// <summary>
    /// Gets the test class that is being executed.
    /// </summary>
    IXunitTestClassV3 TestClass { get; }
}
