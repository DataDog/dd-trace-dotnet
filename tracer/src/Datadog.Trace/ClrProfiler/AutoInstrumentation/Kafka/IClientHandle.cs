// <copyright file="IClientHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Confluent.Kafka.IClient: the producers and consumers of any key and value types, whose handle gives the
/// cluster id.
/// </summary>
[DuckType("Confluent.Kafka.Producer`2", "Confluent.Kafka")]
[DuckType("Confluent.Kafka.Consumer`2", "Confluent.Kafka")]
internal interface IClientHandle
{
    object? Handle { get; }
}
