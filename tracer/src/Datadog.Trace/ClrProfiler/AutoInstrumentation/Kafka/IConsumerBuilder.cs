// <copyright file="IConsumerBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;

/// <summary>
/// Duck Type for Consumer[TKey, TValue]+Config
/// Interface, as used in generic constraint
/// </summary>
internal interface IConsumerBuilder
{
    IEnumerable<KeyValuePair<string, string>> Config { get; }

    ValueWithType<Delegate> OffsetsCommittedHandler { get; set; }
}
