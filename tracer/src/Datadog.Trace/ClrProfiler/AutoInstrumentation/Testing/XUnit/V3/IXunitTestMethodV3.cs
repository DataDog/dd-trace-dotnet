// <copyright file="IXunitTestMethodV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Represents a test class from xUnit.net v3 based on reflection.
/// </summary>
internal interface IXunitTestMethodV3
{
    /// <summary>
    /// Gets the method that this test method refers to.
    /// </summary>
    /// <remarks>
    /// This should only be used to execute a test method. All reflection should be abstracted here
    /// instead for better testability.
    /// </remarks>
    MethodInfo Method { get; }

    /// <summary>
    /// Gets the parameters of the test method.
    /// </summary>
    IReadOnlyCollection<ParameterInfo> Parameters { get; }

    /// <summary>
    /// Gets the return type of the test method.
    /// </summary>
    Type ReturnType { get; }

    /// <summary>
    /// Gets the arguments that will be passed to the test method.
    /// </summary>
    object?[] TestMethodArguments { get; }

    /// <summary>
    /// Gets the test class that this test method belongs to.
    /// </summary>
    IXunitTestClassV3 TestClass { get; }

    /// <summary>
    /// Gets the unique ID for this test method.
    /// </summary>
    /// <remarks>
    /// The unique identifier for a test method should be able to discriminate among test methods in the
    /// same test assembly. This identifier should remain stable until such time as the developer changes
    /// some fundamental part of the identity (assembly, collection, test class, or test method).
    /// Recompilation of the test assembly is reasonable as a stability changing event.
    /// </remarks>
    string UniqueID { get; }
}
