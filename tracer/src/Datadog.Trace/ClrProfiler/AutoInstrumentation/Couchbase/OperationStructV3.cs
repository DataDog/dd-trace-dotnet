// <copyright file="OperationStructV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    /// <summary>
    /// Ducktyping of Couchbase.Core.Operations.IOperation in V3
    /// </summary>
    [DuckCopy]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct OperationStructV3
    {
        /// <summary>
        /// Gets the Operation Code
        /// </summary>
        public OperationCode OpCode;

        /// <summary>
        /// Gets the Operation Key
        /// </summary>
        public string Key;

        /// <summary>
        /// Bucket name, if applicable.
        /// </summary>
        public string BucketName;
    }
}
