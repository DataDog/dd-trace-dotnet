// <copyright file="IInvocationRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK;

/// <summary>
/// Interface that contains all the information necessary to handle an invocation of an AWS Lambda function.
/// This is the DuckType for Amazon.Lambda.RuntimeSupport.InvocationRequest
/// </summary>
internal interface IInvocationRequest : IDisposable
{
    /// <summary>
    /// Gets or sets input to the function invocation.
    /// </summary>
    public Stream InputStream { get; internal set; }

    /// <summary>
    /// Gets or sets context for the invocation.
    /// </summary>
    public ILambdaContext LambdaContext { get; internal set; }
}
