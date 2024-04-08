// <copyright file="TestInvokerStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// TestInvoker`1 structure
/// </summary>
[DuckCopy]
internal struct TestInvokerStruct
{
    /// <summary>
    /// Test class Type
    /// </summary>
    public Type? TestClass;

    /// <summary>
    /// Test method MethodInfo
    /// </summary>
    public MethodInfo? TestMethod;

    /// <summary>
    /// Test method arguments
    /// </summary>
    public object[]? TestMethodArguments;

    /// <summary>
    /// Test case
    /// </summary>
    public TestCaseStruct TestCase;

    /// <summary>
    /// Exception aggregator
    /// </summary>
    public IExceptionAggregator? Aggregator;

    /// <summary>
    /// Message Bus
    /// </summary>
    public object MessageBus;
}
