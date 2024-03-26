// <copyright file="ContainerStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.Container for duck typing
    /// </summary>
    /// <remarks>
    /// https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/Container/Container.cs
    /// </remarks>
    [DuckCopy]
    internal struct ContainerStruct
    {
        /// <summary>
        /// Gets the Id of the Cosmos container
        /// </summary>
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public string Id;

        /// <summary>
        /// Gets the parent Database reference
        /// </summary>
        /// <remarks>
        /// This is an <see langword="object"/> due to a change in in the Microsoft.Azure.Cosmos.ContainerCore object that this DuckTypes.
        /// <p>
        /// In v3.6.0 the Database did not directly contain the <see cref="CosmosClientStruct"/> and instead has a <see cref="CosmosContextClientStruct"/>.
        /// The Database class got a direct reference to the <see cref="CosmosClientStruct"/> starting in v3.9.0
        /// So we can use an <see langword="object"/> here to DuckCast as a <see cref="ContainerStruct"/> and <em>then</em>
        /// attempt to DuckCast this to either <see cref="DatabaseOldStruct"/> or <see cref="DatabaseNewStruct"/>.
        /// </p>
        /// </remarks>
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public object Database;
    }
}
