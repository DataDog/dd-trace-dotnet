// <copyright file="BsonDocumentCommandStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Iast.Aspects.MongoDB.DuckTyping;

/// <summary>
/// MongoDB.Driver.BsonDocumentCommand
/// https://github.com/mongodb/mongo-csharp-driver/blob/7be56f72ff484ffdeb9f021a8d582b4b6d62eeb8/src/MongoDB.Driver/Command.cs#L102
/// </summary>
[DuckCopy]
internal struct BsonDocumentCommandStruct
{
    /// <summary>
    /// Gets the Bson document object from command
    /// </summary>
    public object Document;
}
