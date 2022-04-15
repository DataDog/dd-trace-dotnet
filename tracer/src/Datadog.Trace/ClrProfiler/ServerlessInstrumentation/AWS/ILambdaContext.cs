// <copyright file="ILambdaContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal interface ILambdaContext
    {
        /// <summary>
        /// Gets the lambda context client
        /// Contains the trace context
        /// Used with the datadog ducktyping library
        /// </summary>
        /// <returns>The client context</returns>
        IClientContext ClientContext { get; }
    }
}
