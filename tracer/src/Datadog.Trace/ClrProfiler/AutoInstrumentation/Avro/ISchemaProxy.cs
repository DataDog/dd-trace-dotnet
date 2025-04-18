// <copyright file="ISchemaProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Avro;

/// <summary>
/// DuckTyping interface for Avro.Schema
/// </summary>
internal interface ISchemaProxy : IDuckType
{
    /// <summary>
    /// Gets a value of System.String
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Gets a value of System.String
    /// </summary>
    string? Fullname { get; }
}

/// <summary>
/// DuckTyping interface for Avro.Generic.PreresolvingDatumReader`1
/// </summary>
internal interface IPreresolvingDatumReaderProxy : IDuckType
{
    ISchemaProxy ReaderSchema { get; }

    ISchemaProxy WriterSchema { get; }
}

/// <summary>
/// DuckTyping interface for Avro.Generic.PreresolvingDatumWriter`1
/// </summary>
internal interface IPreresolvingDatumWriterProxy : IDuckType
{
    ISchemaProxy Schema { get; }
}
