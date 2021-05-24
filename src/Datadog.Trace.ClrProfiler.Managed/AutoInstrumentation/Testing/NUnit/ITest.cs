// <copyright file="ITest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.Test
    /// </summary>
    public interface ITest
    {
        /// <summary>
        /// Gets the test name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a MethodInfo for the method implementing this test.
        /// Returns null if the test is not implemented as a method.
        /// </summary>
        IMethodInfo Method { get; }

        /// <summary>
        /// Gets the arguments to use in creating the test or empty array if none required.
        /// </summary>
        object[] Arguments { get; }

        /// <summary>
        /// Gets the properties for this test
        /// </summary>
        IPropertyBag Properties { get; }
    }
}
