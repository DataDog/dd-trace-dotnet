// <copyright file="IResponseMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Net;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.ResponseMessage interface for duck typing
    /// </summary>
    /// <remarks>
    /// https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Handler/ResponseMessage.cs
    /// </remarks>
    internal interface IResponseMessage : IDuckType
    {
        HttpStatusCode StatusCode { get; }

        IResponseHeaders? Headers { get; }
    }
}
