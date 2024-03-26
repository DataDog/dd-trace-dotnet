// <copyright file="DatabaseOldStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.Database for duck typing
    /// </summary>
    /// <remarks>
    /// https://github.com/Azure/azure-cosmos-dotnet-v3/blob/45e5f917ea1959b71240eedf226ae89a18951dd0/Microsoft.Azure.Cosmos/src/Resource/Database/DatabaseCore.cs
    /// </remarks>
    [DuckCopy]
    internal struct DatabaseOldStruct
    {
        /// <summary>
        /// Gets the Id of the Cosmos database
        /// </summary>
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public string Id;

        /// <summary>
        /// Gets the parent Cosmos client instance related the database instance
        /// </summary>
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public CosmosContextClientStruct ClientContext;
    }
}
