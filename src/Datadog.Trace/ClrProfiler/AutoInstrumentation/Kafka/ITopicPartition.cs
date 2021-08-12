﻿// <copyright file="ITopicPartition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// TopicPartition interface for duck-typing
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ITopicPartition
    {
        /// <summary>
        ///     Gets the Kafka topic name.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        ///     Gets the Kafka partition.
        /// </summary>
        public Partition Partition { get; }
    }
}
