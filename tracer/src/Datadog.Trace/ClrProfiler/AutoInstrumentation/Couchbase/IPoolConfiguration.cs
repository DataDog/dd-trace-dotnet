// <copyright file="IPoolConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase;

/// <summary>
/// Ducktyping of Couchbase.Configuration.Client.PoolConfiguration
/// </summary>
internal interface IPoolConfiguration
{
    IClientConfiguration ClientConfiguration { get; }
}
