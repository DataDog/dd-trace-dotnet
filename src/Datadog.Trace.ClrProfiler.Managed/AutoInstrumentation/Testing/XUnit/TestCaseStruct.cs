// <copyright file="TestCaseStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit
{
    /// <summary>
    /// TestCase structure
    /// </summary>
    [DuckCopy]
    public struct TestCaseStruct
    {
        /// <summary>
        /// Display name
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Traits dictionary
        /// </summary>
        public Dictionary<string, List<string>> Traits;
    }
}
