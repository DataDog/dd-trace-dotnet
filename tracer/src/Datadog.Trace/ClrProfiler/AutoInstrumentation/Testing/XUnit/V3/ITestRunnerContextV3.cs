// <copyright file="ITestRunnerContextV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

internal interface ITestRunnerContextV3
{
    /// <summary>
    /// Gets the method that this test originated in.
    /// </summary>
    public MethodInfo TestMethod { get; }

    /// <summary>
    /// Gets the arguments to be passed to the test method during invocation.
    /// </summary>
    public object?[] TestMethodArguments { get; }

    /// <summary>
    /// Gets the test that's being invoked.
    /// </summary>
    public ITestV3 Test { get; }

    /// <summary>
    /// Gets the aggregator used for reporting exceptions.
    /// </summary>
    public IExceptionAggregator? Aggregator { get; }

    /// <summary>
    /// Gets the message bus to send execution engine messages to.
    /// </summary>
    public object MessageBus { get; }

    /// <summary>
    /// Gets the runtime skip reason for the test.
    /// </summary>
    /// <param name="exception">The exception that was thrown during test invocation</param>
    /// <returns>The skip reason, if the test is skipped; <c>null</c>, otherwise</returns>
    public string? GetSkipReason(Exception? exception);
}

/*
internal interface ITestMethodMetadataV3
{
    /// <summary>
    /// Gets the name of the test method that is associated with this message.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the trait values associated with this test method (and the test class,
    /// test collection, and test assembly). If there are none, or the framework does
    /// not support traits, this returns an empty dictionary (not <c>null</c>).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits { get; }

    /// <summary>
    /// Gets the test class that this test method belongs to.
    /// </summary>
    public ITestClassMetadataV3 TestClass { get; }
}
*/
