// <copyright file="IS3EtagResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;

/// <summary>
/// Shared interface for ducktyping for PutObjectResponse, CopyObjectResponse, and
/// CompleteMultipartUploadResponse.
/// </summary>
internal interface IS3EtagResponse : IDuckType
{
    /// <summary>
    /// Gets the S3 response eTag, which is usually wrapped in quotes.
    /// </summary>
    [Duck(Name = "ETag")]
    string? ETag { get; }
}
