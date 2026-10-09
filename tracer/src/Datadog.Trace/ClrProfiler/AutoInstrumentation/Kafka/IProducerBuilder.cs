// <copyright file="IProducerBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Producer[TKey, TValue]+Config
/// Interface, as used in generic constraint
/// </summary>
[DuckType("Confluent.Kafka.ProducerBuilder`2[[Confluent.Kafka.Null, Confluent.Kafka],[Confluent.Kafka.Null, Confluent.Kafka]]", "Confluent.Kafka")]
[DuckType("Confluent.Kafka.ProducerBuilder`2[[System.String, System.Private.CoreLib],[System.String, System.Private.CoreLib]]", "Confluent.Kafka")]
internal interface IProducerBuilder
{
    IEnumerable<KeyValuePair<string, string>> Config { get; }
}
