// <copyright file="IBsonSerializerExtensionsDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects.MongoDB.DuckTyping;

/// <summary>
///     Duck type interface for MongoDB.Bson.Serialization.IBsonSerializerExtensions
///     https://github.com/mongodb/mongo-csharp-driver/blob/v2.8.0/src/MongoDB.Bson/Serialization/IBsonSerializerExtensions.cs
/// </summary>
internal interface IBsonSerializerExtensionsDuckType
{
    /// <summary>
    ///     Deserializes a value.
    /// </summary>
    /// <param name="serializer">The serializer.</param>
    /// <param name="context">The deserialization context.</param>
    /// <returns>A deserialized value.</returns>
    [Duck(Name = "Deserialize", ParameterTypeNames = new[] { "MongoDB.Bson.Serialization.IBsonSerializer, MongoDB.Bson", "MongoDB.Bson.Serialization.BsonDeserializationContext, MongoDB.Bson" })]
    object Deserialize(object serializer, object context);
}
