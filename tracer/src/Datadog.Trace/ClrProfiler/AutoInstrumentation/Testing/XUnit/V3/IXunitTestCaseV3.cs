// <copyright file="IXunitTestCaseV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

internal interface IXunitTestCaseV3
{
    /// <summary>
    /// Gets the display text for the reason a test that might being skipped.
    /// </summary>
    /// <remarks>
    /// This differs from the contract of ITestCaseMetadata.SkipReason by virtue
    /// of the fact that when this value is non-<c>null</c>, it may indicate that a test is
    /// statically skipped (if both SkipUnless and SkipWhen are
    /// <c>null</c>) or dynamically skipped (if one is non-<c>null</c>).
    /// </remarks>
    string SkipReason { get; }

    /// <summary>
    /// Gets the test class that this test case belongs to.
    /// </summary>
    IXunitTestClassV3 TestClass { get; }

    /// <summary>
    /// Gets the full name of the class where the test is defined (i.e. <see cref="Type.FullName"/>).
    /// </summary>
    string TestClassName { get; }

    /// <summary>
    /// Gets the simple name of the class where the test is defined (i.e. <see cref="MemberInfo.Name"/>).
    /// </summary>
    string TestClassSimpleName { get; }

    /// <summary>
    /// Gets the test method this test case belongs to.
    /// </summary>
    IXunitTestMethodV3 TestMethod { get; }

    /// <summary>
    /// Gets the method name where the test is defined.
    /// </summary>
    string TestMethodName { get; }

    /// <summary>
    /// Gets the types for the test method parameters.
    /// </summary>
    /// <remarks>
    /// The values here are formatted according to
    /// <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md">VSTest rules</see>
    /// in order to support Test Explorer. Note that this is not the same as <see cref="Type.FullName"/>.
    /// </remarks>
    string[] TestMethodParameterTypesVSTest { get; }

    /// <summary>
    /// Gets the test method return type.
    /// </summary>
    /// <remarks>
    /// The value here is formatted according to
    /// <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md">VSTest rules</see>
    /// in order to support Test Explorer. Note that this is not the same as <see cref="Type.FullName"/>.
    /// </remarks>
    string TestMethodReturnTypeVSTest { get; }
}
