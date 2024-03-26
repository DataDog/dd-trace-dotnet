// <copyright file="CosmosContextClientStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.CosmosClientContext for duck typing
    /// </summary>
    /// <remarks>
    /// https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/CosmosClientContext.cs
    /// </remarks>
    [DuckCopy]
    internal struct CosmosContextClientStruct
    {
        /// <summary>
        /// Gets the CosmosClient
        /// </summary>
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public CosmosClientStruct Client;
    }
}
