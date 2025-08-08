// <copyright file="ICopyObjectRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.ObjectManagement;

/// <summary>
/// CopyObjectRequest interface for ducktyping.
/// Mirrors Amazon.S3.Model.CopyObjectRequest with unused values removed.
/// </summary>
internal interface ICopyObjectRequest : IDuckType
{
    /// <summary>
    /// Gets the S3 destination bucket name.
    /// </summary>
    [Duck(Name = "DestinationBucket")]
    string? DestinationBucketName { get; }

    /// <summary>
    /// Gets the S3 destination key.
    /// </summary>
    [Duck(Name = "DestinationKey")]
    string? DestinationObjectKey { get; }
}
