// <copyright file="ITestCaseV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Represents a single test case in the system. This test case usually represents a single test, but in
/// the case of dynamically generated data for data driven tests, the test case may actually return
/// multiple results when run.
/// </summary>
internal interface ITestCaseV3
{
    /// <summary>
    /// Gets the test class that this test case belongs to; may be <c>null</c> if the test isn't backed by
    /// a class.
    /// </summary>
    public ITestClassMetadataV3? TestClass { get; }

/*
    /// <summary>
    /// Gets the test collection this test case belongs to. Must be the same instance returned
    /// via <see cref="TestMethod"/> and/or <see cref="TestClass"/> when they are not <c>null</c>.
    /// </summary>
    public object TestCollection { get; }

    /// <summary>
    /// Gets the test method this test case belongs to; may be <c>null</c> if the test isn't backed by
    /// a method.
    /// </summary>
    public ITestMethodMetadataV3? TestMethod { get; }
*/
}
