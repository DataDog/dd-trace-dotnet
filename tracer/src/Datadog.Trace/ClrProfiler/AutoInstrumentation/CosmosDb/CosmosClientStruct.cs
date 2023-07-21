// <copyright file="CosmosClientStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.CosmosClient
    /// </summary>
    /// <remarks>
    /// https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/CosmosClient.cs
    /// </remarks>
    [DuckCopy]
    internal struct CosmosClientStruct
    {
        /// <summary>
        /// Gets the endpoint Uri for the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The Uri for the account endpoint.
        /// </value>
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public Uri Endpoint;
    }
}
