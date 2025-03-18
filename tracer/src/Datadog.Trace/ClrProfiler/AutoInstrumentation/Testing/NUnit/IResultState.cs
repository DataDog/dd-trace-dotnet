// <copyright file="IResultState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

/// <summary>
/// DuckTyping interface for NUnit.Framework.Interfaces.ResultState
/// </summary>
internal interface IResultState
{
    [DuckField(Name = "Ignored", BindingFlags = BindingFlags.Public | BindingFlags.Static)]
    IResultState StaticIgnored { get; }

    /// <summary>
    /// Gets the TestStatus for the test.
    /// </summary>
    /// <value>The status.</value>
    TestStatus Status { get; }

    /// <summary>
    /// Gets the stage of test execution in which
    /// the failure or other result took place.
    /// </summary>
    FailureSite Site { get; }
}
