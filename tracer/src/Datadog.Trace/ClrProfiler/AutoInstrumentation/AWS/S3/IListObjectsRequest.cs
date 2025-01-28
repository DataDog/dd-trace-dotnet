// <copyright file="IListObjectsRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3;

/// <summary>
/// ListObjectsRequest interface for ducktyping.
/// Mirrors Amazon.S3.Model.ListObjectsRequest with unused values removed.
/// </summary>
internal interface IListObjectsRequest : IDuckType
{
    /// <summary>
    /// Gets the S3 bucket name.
    /// </summary>
    [DuckField(Name = "bucketName")]
    string BucketName { get; }
}
