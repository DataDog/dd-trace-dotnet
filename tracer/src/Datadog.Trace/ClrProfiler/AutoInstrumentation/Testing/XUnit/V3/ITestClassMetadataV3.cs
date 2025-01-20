// <copyright file="ITestClassMetadataV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

internal interface ITestClassMetadataV3
{
    /// <summary>
    /// Gets the full name of the test class (i.e., <see cref="Type.FullName"/>).
    /// </summary>
    public string TestClassName { get; }

/*
    /// <summary>
    /// Gets the namespace of the class where the test is defined. Will return <c>null</c> for
    /// classes not residing in a namespace.
    /// </summary>
    public string? TestClassNamespace { get; }

    /// <summary>
    /// Gets the simple name of the test class (the class name without namespace).
    /// </summary>
    public string TestClassSimpleName { get; }

    /// <summary>
    /// Gets the trait values associated with this test class (and the test collection, and test
    /// assembly). If there are none, or the framework does not support traits, this returns an
    /// empty dictionary (not <c>null</c>).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits { get; }

    /// <summary>
    /// Gets the test collection this test class belongs to.
    /// </summary>
    public object TestCollection { get; }
*/
}
