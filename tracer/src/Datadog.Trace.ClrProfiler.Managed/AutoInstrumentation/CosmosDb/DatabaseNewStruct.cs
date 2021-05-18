// <copyright file="DatabaseNewStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.Database for duct typing
    /// </summary>
    [DuckCopy]
    public struct DatabaseNewStruct
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
        public CosmosClientStruct Client;
    }
}
