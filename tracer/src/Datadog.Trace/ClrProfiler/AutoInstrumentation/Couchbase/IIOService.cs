// <copyright file="IIOService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// ReSharper disable InconsistentNaming
#nullable  enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase;

/// <summary>
/// Ducktyping of Couchbase.IO.IIOService
/// </summary>
internal interface IIOService
{
    IConnectionPool ConnectionPool { get; }
}
