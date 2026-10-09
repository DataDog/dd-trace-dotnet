// <copyright file="Statement.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Aerospike
{
    [DuckCopy]
    [DuckCopy("Aerospike.Client.Statement", "AerospikeClient")]
    internal struct Statement
    {
        [DuckField(Name = "setName")]
        public string SetName;

        [DuckField(Name = "ns")]
        public string Ns;
    }
}
