// <copyright file="ITestInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// TestInvoker`1 interface
/// </summary>
internal interface ITestInvoker : IDuckType
{
    /// <summary>
    /// Gets the Test class Type
    /// </summary>
    Type? TestClass { get; }

    /// <summary>
    /// Gets the Test method MethodInfo
    /// </summary>
    MethodInfo? TestMethod { get; }

    /// <summary>
    /// Gets the Test method arguments
    /// </summary>
    object[]? TestMethodArguments { get; }

    /// <summary>
    /// Gets the Test case
    /// </summary>
    ITestCase TestCase { get; }

    /// <summary>
    /// Gets the Exception aggregator
    /// </summary>
    IExceptionAggregator? Aggregator { get; }

    /// <summary>
    /// Gets the Message Bus
    /// </summary>
    object MessageBus { get; }
}
