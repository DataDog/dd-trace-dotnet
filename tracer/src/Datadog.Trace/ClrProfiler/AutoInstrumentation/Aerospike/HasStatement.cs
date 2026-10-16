// <copyright file="HasStatement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike
{
    [DuckCopy]
    [DuckCopy("Aerospike.Client.QueryPartitionCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.AsyncQuery", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.AsyncQueryPartition", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.QueryAggregateCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.QueryListenerCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.QueryRecordCommand", "AerospikeClient")]
    [DuckCopy("Aerospike.Client.ServerCommand", "AerospikeClient")]
    internal struct HasStatement
    {
        [DuckField(Name = "statement")]
        public Statement Statement;
    }
}
