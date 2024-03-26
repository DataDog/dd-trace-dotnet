// <copyright file="IInvocationResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;

/// <summary>
/// Interface that contains the response for an invocation of an AWS Lambda function.
/// This is the DuckType for Amazon.Lambda.RuntimeSupport.InvocationResponse
/// </summary>
internal interface IInvocationResponse
{
    /// <summary>
    /// Gets or sets output from the function invocation.
    /// </summary>
    public Stream OutputStream { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether true if the LambdaBootstrap should dispose the stream after it's read, false otherwise.
    /// Set this to false if you plan to reuse the same output stream for multiple invocations of the function.
    /// </summary>
    public bool DisposeOutputStream { get; internal set; }
}
