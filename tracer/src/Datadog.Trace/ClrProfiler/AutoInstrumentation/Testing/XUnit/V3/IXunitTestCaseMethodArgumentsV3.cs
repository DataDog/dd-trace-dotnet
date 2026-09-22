// <copyright file="IXunitTestCaseMethodArgumentsV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Optional xUnit v3 test case proxy for implementations that expose row-specific arguments.
/// </summary>
internal interface IXunitTestCaseMethodArgumentsV3 : IDuckType
{
    /// <summary>
    /// Gets the arguments that will be passed to the test method for this test case.
    /// </summary>
    object?[]? TestMethodArguments { get; }
}
