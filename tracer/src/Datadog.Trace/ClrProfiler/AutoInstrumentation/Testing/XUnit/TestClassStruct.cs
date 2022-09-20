// <copyright file="TestClassStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// Xunit.Sdk.TestClass proxy structure
    /// </summary>
    [DuckCopy]
    internal struct TestClassStruct
    {
        /// <summary>
        /// Class type info
        /// </summary>
        public TypeInfoStruct Class;
    }
}
