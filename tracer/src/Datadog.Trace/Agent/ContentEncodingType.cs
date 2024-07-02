// <copyright file="ContentEncodingType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Agent;

/// <summary>
/// The encoding used on the content body.
/// <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Encoding">Mozilla</see> for the content-encoding header
/// </summary>
internal enum ContentEncodingType
{
    /// <summary>
    /// The Content-Encoding header is not present or was empty
    /// </summary>
    None,

    /// <summary>
    /// The Content-Encoding header specified 'gzip'
    /// </summary>
    GZip,

    /// <summary>
    /// The Content-Encoding header specified 'deflate'
    /// </summary>
    Deflate,

    /// <summary>
    /// The Content-Encoding header specified 'compress'
    /// </summary>
    Compress,

    /// <summary>
    /// The Content-Encoding header specified 'br'
    /// </summary>
    Brotli,

    /// <summary>
    /// The Content-Encoding header was not recognized
    /// </summary>
    Other,

    /// <summary>
    /// The Content-Encoding header indicated multiple headers were specified
    /// </summary>
    Multiple,
}
