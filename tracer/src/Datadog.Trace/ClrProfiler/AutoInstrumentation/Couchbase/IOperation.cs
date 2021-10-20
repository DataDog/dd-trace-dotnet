// <copyright file="IOperation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using System.Net;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    /// <summary>
    /// Couchbase.IO.Operations.IOperation interface for ducktyping
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IOperation
    {
        /// <summary>
        /// Gets the Operation Code
        /// </summary>
        OperationCode OperationCode { get; }

        /// <summary>
        /// Gets the Operation Key
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the Operation Code
        /// </summary>
        IPEndPoint CurrentHost { get; }
    }
}
