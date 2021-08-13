// <copyright file="QueryDefinitionStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.QueryDefinition for duck typing
    /// </summary>
    [DuckCopy]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct QueryDefinitionStruct
    {
        /// <summary>
        /// Gets the text of the Azure Cosmos DB SQL query.
        /// </summary>
        /// <value>The text of the SQL query.</value>
        [Duck(BindingFlags = DuckAttribute.DefaultFlags | BindingFlags.IgnoreCase)]
        public string QueryText;
    }
}
