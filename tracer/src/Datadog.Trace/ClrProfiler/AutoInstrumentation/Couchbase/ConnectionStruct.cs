// <copyright file="ConnectionStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase;

/// <summary>
/// Ducktyping of Couchbase.IO.IConnection
/// </summary>
[DuckCopy]
internal struct ConnectionStruct
{
    /// <summary>
    /// Gets the PoolConfiguration for the connection
    /// </summary>
    [DuckField(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
    public IPoolConfiguration Configuration;
}
