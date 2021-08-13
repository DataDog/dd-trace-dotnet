// <copyright file="ITestMethod.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// TestMethod ducktype interface
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ITestMethod
    {
        /// <summary>
        /// Gets the test method name
        /// </summary>
        string TestMethodName { get; }

        /// <summary>
        /// Gets the test class name
        /// </summary>
        string TestClassName { get; }

        /// <summary>
        /// Gets the MethodInfo
        /// </summary>
        MethodInfo MethodInfo { get; }

        /// <summary>
        /// Gets the test arguments
        /// </summary>
        object[] Arguments { get; }

        /// <summary>
        /// Gets all attributes
        /// </summary>
        /// <param name="inherit">Injerits all the attributes from base classes</param>
        /// <returns>Attribute array</returns>
        Attribute[] GetAllAttributes(bool inherit);
    }
}
