// <copyright file="IHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Minimal duck-typing interface for MassTransit headers collection
/// Supports extracting trace context from TransportHeaders or Headers
/// </summary>
internal interface IHeaders
{
    /// <summary>
    /// Gets all header key/value pairs
    /// Returns IEnumerable of HeaderValue structs or KeyValuePair structures
    /// Using object to allow flexibility across different header implementations
    /// </summary>
    System.Collections.IEnumerable GetAll();
}
