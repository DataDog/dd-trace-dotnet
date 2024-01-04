// <copyright file="IBsonSerializerDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects.MongoDB.DuckTyping;

/// <summary>
/// A static class that represents the BSON serialization functionality.
/// </summary>
public interface IBsonSerializerDuckType
{
    /// <summary>
    /// Deserializes a value.
    /// </summary>
    /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
    /// <param name="bsonReader">The BsonReader.</param>
    /// <param name="configurator">The configurator.</param>
    /// <returns>A deserialized value.</returns>
    [Duck(Name = "Deserialize", ParameterTypeNames = new[] { "MongoDB.Bson.IO.IBsonReader, MongoDB.Bson", "Action`1" }, GenericParameterTypeNames = new[] { "TNominalType" })]
    TNominalType Deserialize<TNominalType>(object bsonReader, object configurator = null);
}
