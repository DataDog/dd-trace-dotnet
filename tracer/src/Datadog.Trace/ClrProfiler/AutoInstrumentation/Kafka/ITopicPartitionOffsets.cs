// <copyright file="ITopicPartitionOffsets.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

[DuckType("System.Collections.Generic.List`1[[Confluent.Kafka.TopicPartitionOffset, Confluent.Kafka]]", "System.Private.CoreLib")]
[DuckType("System.Collections.Generic.List`1[[Confluent.Kafka.TopicPartitionOffsetError, Confluent.Kafka]]", "System.Private.CoreLib")]
internal interface ITopicPartitionOffsets
{
    /// <summary>
    /// Gets number of values
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the header at the specified index
    /// </summary>
    public ITopicPartitionOffset this[int index] { get;  }
}
