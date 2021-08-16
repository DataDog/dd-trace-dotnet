// <copyright file="TestPropertyAttributeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// TestPropertyAttribute ducktype struct
    /// </summary>
    [DuckCopy]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct TestPropertyAttributeStruct
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name;

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value;
    }
}
