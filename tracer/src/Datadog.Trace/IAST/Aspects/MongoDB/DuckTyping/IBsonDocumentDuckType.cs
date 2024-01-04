// <copyright file="IBsonDocumentDuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Iast.Aspects.MongoDB.DuckTyping;

/// <summary>
///     Duck type for a BSON document.
/// </summary>
public interface IBsonDocumentDuckType
{
    /// <summary>
    ///     Parses a JSON string and returns a BsonDocument.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>A BsonDocument.</returns>
    /// [Duck(Name = "Parse", ParameterTypeNames = new[] { "System.String" })]
    object Parse(string json);
}
