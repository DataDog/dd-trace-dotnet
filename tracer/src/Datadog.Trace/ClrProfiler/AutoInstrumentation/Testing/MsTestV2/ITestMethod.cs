// <copyright file="ITestMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

/// <summary>
/// TestMethod ducktype interface
/// </summary>
internal interface ITestMethod : IDuckType
{
    /// <summary>
    /// Gets the test method name
    /// </summary>
    string? TestMethodName { get; }

    /// <summary>
    /// Gets the test class name
    /// </summary>
    string? TestClassName { get; }

    /// <summary>
    /// Gets the MethodInfo
    /// </summary>
    MethodInfo? MethodInfo { get; }

    /// <summary>
    /// Gets the test arguments
    /// </summary>
    object[]? Arguments { get; }
}

/// <summary>
/// TestMethod ducktype interface
/// </summary>
internal interface ITestMethodV3 : ITestMethod
{
    /// <summary>
    /// Gets all attributes
    /// </summary>
    /// <param name="inherit">Injerits all the attributes from base classes</param>
    /// <returns>Attribute array</returns>
    Attribute[]? GetAllAttributes(bool inherit);

    /// <summary>
    /// Invokes the test method.
    /// </summary>
    /// <param name="arguments">
    /// Arguments to pass to test method. (E.g. For data driven).
    /// </param>
    /// <returns>
    /// Result of test method invocation.
    /// </returns>
    /// <remarks>
    /// This call handles asynchronous test methods as well.
    /// </remarks>
    object? Invoke(object[]? arguments);
}

/// <summary>
/// TestMethod v4 ducktype interface
/// </summary>
internal interface ITestMethodV4 : ITestMethod
{
    /// <summary>
    /// Gets all attributes
    /// </summary>
    /// <returns>Attribute array</returns>
    Attribute[]? GetAllAttributes();

    /// <summary>
    /// Invokes the test method.
    /// </summary>
    /// <param name="arguments">
    /// Arguments to pass to test method. (E.g. For data driven).
    /// </param>
    /// <returns>
    /// Result of test method invocation.
    /// </returns>
    /// <remarks>
    /// This call handles asynchronous test methods as well.
    /// </remarks>
    IDuckTypeTask<object> InvokeAsync(object[]? arguments);
}
