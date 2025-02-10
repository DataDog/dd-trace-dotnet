// <copyright file="IXunitTestV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Represents a test from xUnit.net v3 based on reflection.
/// </summary>
internal interface IXunitTestV3
{
    /// <summary>
    /// Gets a skip reason for this test.
    /// </summary>
    /// <remarks>
    /// This value may not line up the with IXunitTestCase.SkipReason, as you can skip
    /// individual data rows during delay enumeration.
    /// </remarks>
    string? SkipReason { get; }

    /// <summary>
    /// Gets the test method to run. May different from the test method embedded in the test case.
    /// </summary>
    IXunitTestMethodV3 TestMethod { get; }

    /// <summary>
    /// Gets the arguments to be passed to the test method during invocation.
    /// </summary>
    object?[] TestMethodArguments { get; }

    /// <summary>
    /// Gets the display name of the test.
    /// </summary>
    string TestDisplayName { get; }

    /// <summary>
    /// Gets the trait values associated with this test case. If
    /// there are none, or the framework does not support traits,
    /// this should return an empty dictionary (not <c>null</c>).
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits { get; }

    /// <summary>
    /// Gets the test case this test belongs to.
    /// </summary>
    IXunitTestCaseV3 TestCase { get; }
}
