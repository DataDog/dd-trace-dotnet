// <copyright file="IAmazonWebServiceResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Net;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// AmazonWebServiceResponse interface for ducktyping
    /// </summary>
    internal interface IAmazonWebServiceResponse : IDuckType
    {
        /// <summary>
        /// Gets the length of the content
        /// </summary>
        long ContentLength { get; }

        /// <summary>
        /// Gets the response metadata
        /// </summary>
        IResponseMetadata? ResponseMetadata { get; }

        /// <summary>
        /// Gets the http status code of the AWS request
        /// </summary>
        HttpStatusCode HttpStatusCode { get; }
    }
}
