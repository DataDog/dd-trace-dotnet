// <copyright file="IConnection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Net;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// MongoDB.Driver.Core.IConnection interface for duck-typing
    /// </summary>
    internal interface IConnection : IDuckType
    {
        /// <summary>
        /// Gets the command object passed into the wire protocol
        /// </summary>
        EndPoint? EndPoint { get; }
    }
}
