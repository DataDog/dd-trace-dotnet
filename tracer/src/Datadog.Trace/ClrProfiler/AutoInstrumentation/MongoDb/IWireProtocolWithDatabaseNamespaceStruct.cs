// <copyright file="IWireProtocolWithDatabaseNamespaceStruct.cs" company="Datadog">
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
    internal struct IWireProtocolWithDatabaseNamespaceStruct
    {
        /// <summary>
        /// Gets the database namespace object passed into the wire protocol
        /// </summary>
        [DuckField(Name = "_databaseNamespace")]
        public object? DatabaseNamespace;
    }
}
