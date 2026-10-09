// <copyright file="IConnectionPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable  enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase;

/// <summary>
/// Ducktyping of Couchbase.IO.IConnectionPool
/// </summary>
[DuckType("Couchbase.IO.SharedConnectionPool`1[[Couchbase.IO.MultiplexingConnection, Couchbase.NetClient]]", "Couchbase.NetClient")]
internal interface IConnectionPool
{
    IPoolConfiguration Configuration { get; }
}
