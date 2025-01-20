// <copyright file="TestClassStructV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// TestClass metadata
/// </summary>
[DuckCopy]
internal struct TestClassStructV3
{
    /// <summary>
    /// Gets the full name of the test class (i.e., <see cref="Type.FullName"/>).
    /// </summary>
    public string TestClassName;

    /// <summary>
    /// Gets the namespace of the class where the test is defined. Will return <c>null</c> for
    /// classes not residing in a namespace.
    /// </summary>
    public string? TestClassNamespace;

    /// <summary>
    /// Gets the simple name of the test class (the class name without namespace).
    /// </summary>
    public string TestClassSimpleName;

    /// <summary>
    /// Gets the trait values associated with this test class (and the test collection, and test
    /// assembly). If there are none, or the framework does not support traits, this returns an
    /// empty dictionary (not <c>null</c>).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits;
}
