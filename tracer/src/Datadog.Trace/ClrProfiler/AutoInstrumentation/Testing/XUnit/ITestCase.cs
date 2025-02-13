// <copyright file="ITestCase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;

/// <summary>
/// TestCase interface
/// </summary>
internal interface ITestCase : IDuckType
{
    /// <summary>
    /// Gets the Display name
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Gets the Traits dictionary
    /// </summary>
    Dictionary<string, List<string>?>? Traits { get; }

    /// <summary>
    /// Gets a unique identifier for the test case.
    /// </summary>
    /// <remarks>
    /// The unique identifier for a test case should be able to discriminate
    /// among test cases, even those which are varied invocations against the
    /// same test method (i.e., theories). Ideally, this identifier would remain
    /// stable until such time as the developer changes some fundamental part
    /// of the identity (assembly, class name, test name, or test data); however,
    /// the minimum stability of the identifier must at least extend across
    /// multiple discoveries of the same test in the same (non-recompiled)
    /// assembly.
    /// </remarks>
    string UniqueID { get; }
}
