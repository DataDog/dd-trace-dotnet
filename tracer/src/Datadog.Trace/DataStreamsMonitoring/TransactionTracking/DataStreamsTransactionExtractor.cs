// <copyright file="DataStreamsTransactionExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.Serialization;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

[Serializable]
internal sealed class DataStreamsTransactionExtractor
{
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
            switch (StringType)
            {
                case "HTTP_OUT_HEADERS":
                    return Type.HttpOutHeaders;
                case "HTTP_IN_HEADERS":
                    return Type.HttpInHeaders;
                case "KAFKA_CONSUME_HEADERS":
                    return Type.KafkaConsumeHeaders;
                case "KAFKA_PRODUCE_HEADERS":
                    return Type.KafkaProduceHeaders;
                default:
                    return Type.Unknown;
            }
        }
    }
}
