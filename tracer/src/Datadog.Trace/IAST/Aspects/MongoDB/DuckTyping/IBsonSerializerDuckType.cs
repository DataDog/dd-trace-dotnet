// <copyright file="IBsonSerializerDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects.MongoDB.DuckTyping;

/// <summary>
///     Duck type interface for MongoDB.Bson.Serialization.IBsonSerializer
///     https://github.com/mongodb/mongo-csharp-driver/blob/v2.8.0/src/MongoDB.Bson/Serialization/IBsonSerializer.cs
/// </summary>
internal interface IBsonSerializerDuckType
{
    /// <summary>
    ///     Deserializes a value.
    /// </summary>
    /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
    /// <param name="bsonReader">The BsonReader.</param>
    /// <param name="configurator">The configurator.</param>
    /// <returns>A deserialized value.</returns>
    [Duck(Name = "Deserialize", ParameterTypeNames = new[] { "MongoDB.Bson.IO.IBsonReader, MongoDB.Bson", "Action`1" }, GenericParameterTypeNames = new[] { "TNominalType" })]
    TNominalType Deserialize<TNominalType>(object bsonReader, object configurator);
}
