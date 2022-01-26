// <copyright file="ILambdaRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    /// <summary>
    /// A Span represents a logical unit of work in the system. It may be
    /// related to other spans by parent/children relationships. The span
    /// tracks the duration of an operation as well as associated metadata in
    /// the form of a resource name, a service name, and user defined tags.
    /// </summary>
    public interface ILambdaRequest
    {
        /// <summary>
        /// Get the trace context request
        /// </summary>
        /// <returns>The trace context request</returns>
        WebRequest GetTraceContextRequest();

        /// <summary>
        /// Get the start invocation request
        /// </summary>
        /// <returns>The start invocation request</returns>
        WebRequest GetStartInvocationRequest();

        /// <summary>
        /// Get the end invocation request
        /// </summary>
        /// <returns>The end invocation request</returns>
        WebRequest GetEndInvocationRequest(bool isError);
    }
}
