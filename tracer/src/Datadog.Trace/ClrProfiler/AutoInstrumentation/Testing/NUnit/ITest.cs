// <copyright file="ITest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.Test
    /// </summary>
    internal interface ITest : IDuckType
    {
        /// <summary>
        /// Gets the id of the test
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the test name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the type of the test
        /// </summary>
        string TestType { get; }

        /// <summary>
        /// Gets the fully qualified name of the test
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Gets the name of the class containing this test. Returns
        /// null if the test is not associated with a class.
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// Gets the name of the method implementing this test.
        /// Returns null if the test is not implemented as a method.
        /// </summary>
        string MethodName { get; }

        /// <summary>
        /// Gets the Type of the test fixture, if applicable, or
        /// null if no fixture type is associated with this test.
        /// </summary>
        ITypeInfo TypeInfo { get; }

        /// <summary>
        /// Gets a MethodInfo for the method implementing this test.
        /// Returns null if the test is not implemented as a method.
        /// </summary>
        IMethodInfo Method { get; }

        /// <summary>
        /// Gets or sets whether or not the test should be run
        /// </summary>
        public RunState RunState { get; set; }

        /// <summary>
        /// Gets the count of the test cases ( 1 if this is a test case )
        /// </summary>
        int TestCaseCount { get; }

        /// <summary>
        /// Gets the properties for this test
        /// </summary>
        IPropertyBag Properties { get; }

        /// <summary>
        /// Gets a value indicating whether if the instance is a TestSuite
        /// </summary>
        bool IsSuite { get; }

        /// <summary>
        /// Gets a value indicating whether the current test
        /// has any descendant tests.
        /// </summary>
        bool HasChildren { get; }

        /// <summary>
        /// Gets this test's child tests
        /// </summary>
        System.Collections.IList Tests { get; }

        /// <summary>
        /// Gets a fixture object for running this test.
        /// </summary>
        object Fixture { get; }

        /// <summary>
        /// Gets the arguments to use in creating the test or empty array if none required.
        /// </summary>
        object[] Arguments { get; }

        /// <summary>
        /// Gets the parent test, if any.
        /// </summary>
        /// <value>The parent test or null if none exists.</value>
        ITest Parent { get; }

        /// <summary>
        /// Make test result
        /// </summary>
        /// <returns>TestResult instance</returns>
        ITestResult MakeTestResult();
    }

    internal struct TestAssemblyStruct
    {
        /// <summary>
        /// Gets the assembly in which the type is declared
        /// </summary>
        public Assembly Assembly;
    }
}
