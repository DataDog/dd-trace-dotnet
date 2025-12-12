// <copyright file="DataStreamsTransactionExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.Serialization;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.DataStreamsMonitoring.TransactionTracking;

[Serializable]
internal class DataStreamsTransactionExtractor
{
    public enum Type
    {
        Unknown,

        [EnumMember(Value = "HTTP_OUT_HEADERS")]
        HttpOutHeaders,

        [EnumMember(Value = "HTTP_IN_HEADERS")]
        HttpInHeaders,

        [EnumMember(Value = "KAFKA_CONSUME_HEADERS")]
        KafkaConsumeHeaders,

        [EnumMember(Value = "KAFKA_PRODUCE_HEADERS")]
        KafkaProduceHeaders,
    }

    [JsonProperty(PropertyName = "name")]
    public string Name { get; private set; } = string.Empty;

    [JsonProperty(PropertyName = "type")]
    public Type ExtractorType { get; private set; } = Type.Unknown;

    [JsonProperty(PropertyName = "value")]
    public string Value { get; private set; } = string.Empty;
}
