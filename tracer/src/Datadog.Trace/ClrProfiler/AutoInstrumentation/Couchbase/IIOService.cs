// <copyright file="IIOService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// ReSharper disable InconsistentNaming
#nullable  enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase;

/// <summary>
/// Ducktyping of Couchbase.IO.IIOService
/// </summary>
[DuckType("Couchbase.IO.Services.PooledIOService", "Couchbase.NetClient")]
[DuckType("Couchbase.IO.Services.SharedPooledIOService", "Couchbase.NetClient")]
internal interface IIOService
{
    IConnectionPool ConnectionPool { get; }
}
