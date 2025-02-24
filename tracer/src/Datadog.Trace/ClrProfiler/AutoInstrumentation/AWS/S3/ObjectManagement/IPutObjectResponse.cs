// <copyright file="IPutObjectResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.S3.ObjectManagement;

/// <summary>
/// PutObjectResponse interface for ducktyping.
/// Mirrors Amazon.S3.Model.PutObjectResponse with unused values removed.
/// </summary>
internal interface IPutObjectResponse : IDuckType
{
    /// <summary>
    /// Gets the S3 response eTag, which is usually wrapped in quotes.
    /// </summary>
    [DuckField(Name = "eTag")]
    string ETag { get; }
}
