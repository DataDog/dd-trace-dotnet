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
    private ExtractorType? _cachedType;

    public enum ExtractorType
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

    public ExtractorType ParsedType
    {
        get
        {
            if (_cachedType is null)
            {
                _cachedType = StringType switch
                {
                    "HTTP_OUT_HEADERS" => ExtractorType.HttpOutHeaders,
                    "HTTP_IN_HEADERS" => ExtractorType.HttpInHeaders,
                    "KAFKA_CONSUME_HEADERS" => ExtractorType.KafkaConsumeHeaders,
                    "KAFKA_PRODUCE_HEADERS" => ExtractorType.KafkaProduceHeaders,
                    _ => ExtractorType.Unknown,
                };
            }

            return _cachedType.Value;
        }
    }
}
