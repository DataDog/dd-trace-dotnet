// <copyright file="ITestClassRunner.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections;
using System.Reflection;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

#pragma warning disable SA1201

/// <summary>
/// TestClassRunner`1 structure
/// </summary>
internal interface ITestClassRunner : IDuckType
{
    /// <summary>
    /// Gets test class
    /// </summary>
    TestClassStruct TestClass { get; }

    /// <summary>
    /// Gets test cases
    /// </summary>
    IEnumerable TestCases { get; }

    /// <summary>
    /// Gets or sets the message bus to report run status to.
    /// </summary>
    object MessageBus { get; set; }
}

internal interface ITestMethodTestCase : IDuckType
{
    object? Method { get; }

    object?[] TestMethodArguments { get; }
}

internal interface IReflectionMethodInfo
{
    /// <summary>
    /// Gets the name of the method.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Gets the underlying <see cref="MethodInfo"/> for the method.
    /// </summary>
    MethodInfo? MethodInfo { get; }
}
