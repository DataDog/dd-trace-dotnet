// <copyright file="CosmosClientContextMetadataStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.ClientContextCore metadata for CRUD instrumentation (3.12.0+).
    /// Read fields directly because the property getters throw after disposal.
    /// </summary>
    [DuckCopy]
    internal struct CosmosClientContextMetadataStruct
    {
        [DuckField(Name = "userAgent")]
        public string UserAgent;

        [DuckField(Name = "clientOptions")]
        public CosmosClientOptionsStruct ClientOptions;
    }
}
