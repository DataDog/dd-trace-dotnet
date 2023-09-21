// <copyright file="IBatchRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb
{
    /// <summary>
    /// BatchRequest interface for duck typing.
    /// </summary>
    internal interface IBatchRequest
    {
        /// <summary>
        /// Gets or sets the RequestItems of a Batch request.
        /// </summary>
        IDictionary RequestItems { get; set; }
    }
}
