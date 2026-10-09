// <copyright file="IConsumeResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// ConsumeResult for duck-typing
    /// </summary>
    [DuckType("Confluent.Kafka.ConsumeResult`2[[System.Byte[], System.Private.CoreLib],[System.Byte[], System.Private.CoreLib]]", "Confluent.Kafka")]
    [DuckType("Confluent.Kafka.ConsumeResult`2[[System.String, System.Private.CoreLib],[System.String, System.Private.CoreLib]]", "Confluent.Kafka")]
    internal interface IConsumeResult
    {
        /// <summary>
        /// Gets the topic
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the partition
        /// </summary>
        public Partition Partition { get; }

        /// <summary>
        /// Gets the offset
        /// </summary>
        public Offset Offset { get; }

        /// <summary>
        /// Gets the Kafka record
        /// </summary>
        public IMessage Message { get; }

        /// <summary>
        /// Gets a value indicating whether gets whether the message is a partition EOF
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public bool IsPartitionEOF { get; }
    }
}
