// <copyright file="ITestV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Represents a single test in the system. A test case typically contains only a single test,
/// but may contain many if circumstances warrant it (for example, test data for a theory cannot
/// be pre-enumerated, so the theory yields a single test case with multiple tests).
/// </summary>
internal interface ITestV3
{
    /// <summary>
    /// Gets the display name of the test.
    /// </summary>
    public string TestDisplayName { get; }

    /// <summary>
    /// Gets the trait values associated with this test case. If
    /// there are none, or the framework does not support traits,
    /// this should return an empty dictionary (not <c>null</c>).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits { get; }

    /// <summary>
    /// Gets the test case this test belongs to.
    /// </summary>
    public ITestCaseV3 TestCase { get; }
}
