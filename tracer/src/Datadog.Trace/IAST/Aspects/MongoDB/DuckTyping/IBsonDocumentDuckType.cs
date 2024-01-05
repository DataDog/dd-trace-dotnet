// <copyright file="IBsonDocumentDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Aspects.MongoDB.DuckTyping;

/// <summary>
///     Duck type interface for MongoDB.Bson.BsonDocument
///     https://github.com/mongodb/mongo-csharp-driver/blob/v2.8.0/src/MongoDB.Bson/ObjectModel/BsonDocument.cs
/// </summary>
internal interface IBsonDocumentDuckType
{
    /// <summary>
    ///     Parses a JSON string and returns a BsonDocument.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>A BsonDocument.</returns>
    object Parse(string json);
}
