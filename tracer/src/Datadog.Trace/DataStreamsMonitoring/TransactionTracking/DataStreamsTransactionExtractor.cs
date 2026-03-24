// <copyright file="DataStreamsTransactionExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

internal sealed class DataStreamsTransactionExtractor
{
    // issue 10: static dictionary avoids a switch allocation on every access
    private static readonly Dictionary<string, Type> TypeMap = new(System.StringComparer.Ordinal)
    {
        ["HTTP_OUT_HEADERS"] = Type.HttpOutHeaders,
        ["HTTP_IN_HEADERS"] = Type.HttpInHeaders,
        ["KAFKA_CONSUME_HEADERS"] = Type.KafkaConsumeHeaders,
        ["KAFKA_PRODUCE_HEADERS"] = Type.KafkaProduceHeaders,
    };

    // issue 8: cached so the dictionary lookup runs only once per instance
    private Type? _cachedType;

    public enum Type
    {
        Unknown,

        HttpOutHeaders,

        HttpInHeaders,

        KafkaConsumeHeaders,

        KafkaProduceHeaders,
    }

    [JsonProperty(PropertyName = "name")]
    public string Name { get; private set; } = string.Empty;

    [JsonProperty(PropertyName = "type")]
    public string StringType { get; private set; } = string.Empty;

    [JsonProperty(PropertyName = "value")]
    public string Value { get; private set; } = string.Empty;

    public Type ExtractorType
    {
        get
        {
            if (_cachedType is null)
            {
                _cachedType = TypeMap.TryGetValue(StringType, out var t) ? t : Type.Unknown;
            }

            return _cachedType.Value;
        }
    }
}
