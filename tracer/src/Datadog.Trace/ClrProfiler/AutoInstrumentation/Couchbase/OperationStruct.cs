// <copyright file="OperationStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Net;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    /// <summary>
    /// Ducktyping of Couchbase.IO.Operations.IOperation and generic implementations
    /// </summary>
    [DuckCopy]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct OperationStruct
    {
        /// <summary>
        /// Gets the Operation Code
        /// </summary>
        public OperationCode OperationCode;

        /// <summary>
        /// Gets the Operation Key
        /// </summary>
        public string Key;

        /// <summary>
        /// Gets the Operation Code
        /// </summary>
        public IPEndPoint CurrentHost;
    }
}
