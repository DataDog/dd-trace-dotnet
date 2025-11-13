// <copyright file="IRequestInvokerHandlerProxy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.RequestInvokerHandler interface for duck typing
    /// </summary>
    internal interface IRequestInvokerHandlerProxy : IDuckType
    {
        /// <summary>
        /// Gets the CosmosClient reference
        /// </summary>
        [DuckField(Name = "client")]
        CosmosClientStruct Client { get; }
    }
}
