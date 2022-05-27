// <copyright file="ILambdaExtensionRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Net;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS
{
    internal interface ILambdaExtensionRequest
    {
        /// <summary>
        /// Get the start invocation request
        /// </summary>
        /// <returns>The start invocation request</returns>
        WebRequest GetStartInvocationRequest();

        /// <summary>
        /// Get the end invocation request
        /// </summary>
        /// <returns>The end invocation request</returns>
        WebRequest GetEndInvocationRequest(Scope scope, bool isError);
    }
}
