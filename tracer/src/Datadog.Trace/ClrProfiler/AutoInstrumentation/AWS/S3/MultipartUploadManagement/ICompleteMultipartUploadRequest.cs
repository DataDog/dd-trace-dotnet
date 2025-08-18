// <copyright file="ICompleteMultipartUploadRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.MultipartUploadManagement;

/// <summary>
/// CompleteMultipartUploadRequest interface for ducktyping.
/// Mirrors Amazon.S3.Model.CompleteMultipartUploadRequest with unused values removed.
/// </summary>
internal interface ICompleteMultipartUploadRequest : IDuckType
{
    /// <summary>
    /// Gets the S3 bucket name.
    /// </summary>
    [Duck(Name = "BucketName")]
    string? BucketName { get; }

    /// <summary>
    /// Gets the S3 request key.
    /// </summary>
    [Duck(Name = "Key")]
    string? ObjectKey { get; }
}
