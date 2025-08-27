// <copyright file="IBsonSerializerProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Proxy for https://github.com/mongodb/mongo-csharp-driver/blob/4027d482d14960364c14be1ca59f7f6e350042a3/src/MongoDB.Bson/Serialization/IBsonSerializer.cs#L23
/// </summary>
internal interface IBsonSerializerProxy
{
    [Duck(ParameterTypeNames = ["MongoDB.Bson.Serialization.BsonSerializationContext", "MongoDB.Bson.Serialization.BsonSerializationArgs", ClrNames.Object])]
    void Serialize(object context, object args, object value);
}
