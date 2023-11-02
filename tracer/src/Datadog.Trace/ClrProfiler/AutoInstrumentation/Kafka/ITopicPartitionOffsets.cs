// <copyright file="ITopicPartitionOffsets.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

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
