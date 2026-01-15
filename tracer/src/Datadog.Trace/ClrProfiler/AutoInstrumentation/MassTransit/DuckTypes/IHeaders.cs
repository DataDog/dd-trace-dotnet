// <copyright file="IHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Duck-typing interface for MassTransit.Headers (for reading headers on consume side)
/// JsonTransportHeaders has: GetAll, Get[T](string, T), GetEnumerator
/// Since Get is generic, we use GetAll which returns IEnumerable[HeaderValue]
/// </summary>
internal interface IHeaders
{
    /// <summary>
    /// Gets all header values
    /// </summary>
    /// <returns>Enumerable of all headers</returns>
    IEnumerable<KeyValuePair<string, object>> GetAll();
}
