// <copyright file="HasKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike
{
    [DuckCopy]
    [DuckCopy("Aerospike.Client.AsyncBatchExistsArrayCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.AsyncBatchGetArrayCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.AsyncDelete", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.AsyncExists", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.AsyncRead", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.AsyncWrite", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.BatchExistsArrayCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.BatchGetArrayCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.DeleteCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.ExistsCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.QueryPartitionCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.ReadCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.WriteCommand", "AerospikeClient")]
    internal struct HasKey
    {
        [DuckField(Name = "key")]
        public Key Key;
    }
}
