// <copyright file="IBsonWriterProxyV3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;

/// <summary>
/// Duck Typing interface proxy for: https://github.com/mongodb/mongo-csharp-driver/blob/v3.0.x/src/MongoDB.Bson/IO/IBsonWriter.cs
/// </summary>
internal interface IBsonWriterProxyV3 : IBsonWriterProxy
{
    /// <summary>
    /// Writes a Guid in Standard representation to the writer.
    /// </summary>
    /// <param name="guid">The Guid value.</param>
    void WriteGuid(Guid guid);

    /// <summary>
    /// Writes a Guid in the specified representation to the writer.
    /// </summary>
    /// <param name="guid">The Guid value.</param>
    /// <param name="guidRepresentation">The GuidRepresentation.</param>
    [Duck(ParameterTypeNames = [ClrNames.Guid, "MongoDB.Bson.GuidRepresentation"])]
    void WriteGuid(Guid guid, int guidRepresentation);
}
