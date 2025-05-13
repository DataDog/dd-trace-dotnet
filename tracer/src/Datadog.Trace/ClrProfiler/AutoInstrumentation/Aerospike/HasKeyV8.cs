// <copyright file="HasKeyV8.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike;

/// <summary>
/// Duck type for https://github.com/aerospike/aerospike-client-csharp/blob/a008a41a2c81916f7dd8db2e2757b516bdad77a8/AerospikeClient/Async/AsyncWriteBase.cs#L23
/// Differs from HasKey in that the former reads from a field called "key" and the latter reads from a property called "Key"
/// </summary>
[DuckCopy]
internal struct HasKeyV8
{
    public Key Key;
}
