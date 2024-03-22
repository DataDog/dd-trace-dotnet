// <copyright file="IWorkItem.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.Execution.WorkItem
    /// </summary>
    internal interface IWorkItem : IDuckType
    {
        /// <summary>
        /// Gets the test being executed by the work item
        /// </summary>
        ITest Test { get; }

        /// <summary>
        /// Gets the test result
        /// </summary>
        ITestResult Result { get; }
    }

    internal interface INUnitTestAssemblyRunner
    {
        ITest LoadedTest { get; }

        IWorkItem TopLevelWorkItem { get; }
    }

    internal interface ITypeInfo
    {
        /// <summary>
        /// Gets the underlying Type on which this ITypeInfo is based
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets the name of the Type
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the full name of the Type
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Gets the assembly in which the type is declared
        /// </summary>
        Assembly Assembly { get; }

        /// <summary>
        /// Gets the namespace of the Type
        /// </summary>
        string Namespace { get; }
    }
}
