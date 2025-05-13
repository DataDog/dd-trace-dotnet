// <copyright file="IXunitTestRunnerContextV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// XunitTestRunnerContext proxy
/// </summary>
internal interface IXunitTestRunnerContextV3 : IContextBaseV3
{
    /// <summary>
    /// Gets the arguments that should be passed to the test class when it's constructed.
    /// </summary>
    object?[] ConstructorArguments { get; }

    /// <summary>
    /// Gets the method that this test originated in.
    /// </summary>
    MethodInfo TestMethod { get; }

    /// <summary>
    /// Gets the arguments to be passed to the test method during invocation.
    /// </summary>
    object?[] TestMethodArguments { get; }

    /// <summary>
    /// Gets the test that's being invoked.
    /// </summary>
    IXunitTestV3 Test { get; }

    /// <summary>
    /// Gets the runtime skip reason for the test.
    /// </summary>
    /// <param name="exception">The exception that was thrown during test invocation</param>
    /// <returns>The skip reason, if the test is skipped; <c>null</c>, otherwise</returns>
    string? GetSkipReason(Exception? exception);
}
