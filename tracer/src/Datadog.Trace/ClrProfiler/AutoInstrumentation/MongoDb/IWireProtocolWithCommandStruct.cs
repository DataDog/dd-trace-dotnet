// <copyright file="IWireProtocolWithCommandStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// MongoDB.Driver.Core.IWireProtocol interface for duck-typing
    /// </summary>
    [DuckCopy]
    [DuckCopy("MongoDB.Driver.Core.WireProtocol.CommandUsingCommandMessageWireProtocol`1[[MongoDB.Bson.BsonDocument, MongoDB.Bson]]", "MongoDB.Driver")]
    [DuckCopy("MongoDB.Driver.Core.WireProtocol.CommandUsingQueryMessageWireProtocol`1[[MongoDB.Bson.BsonDocument, MongoDB.Bson]]", "MongoDB.Driver")]
    [DuckCopy("MongoDB.Driver.Core.WireProtocol.CommandWireProtocol`1[[MongoDB.Bson.BsonDocument, MongoDB.Bson]]", "MongoDB.Driver")]
    [DuckCopy("MongoDB.Driver.Core.WireProtocol.CommandWireProtocol`1[[MongoDB.Driver.Core.Operations.AggregateOperation`1+AggregateResult[[MongoDB.Bson.BsonDocument, MongoDB.Bson]], MongoDB.Driver]]", "MongoDB.Driver")]
    internal struct IWireProtocolWithCommandStruct
    {
        /// <summary>
        /// Gets the command object passed into the wire protocol
        /// </summary>
        [DuckField(Name = "_command")]
        public object? Command;
    }
}
