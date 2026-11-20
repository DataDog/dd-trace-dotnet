// <copyright file="ITruncatedContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.AppSec.WafEncoding;

/// <summary>
/// A container that was cut to the WAF container size limit before reaching the encoder, such as the request
/// bodies ObjectExtractor extracts, so that the encoder can still report the truncation
/// </summary>
internal interface ITruncatedContainer
{
    /// <summary>
    /// Gets the element count of the container before it was cut
    /// </summary>
    int OriginalCount { get; }
}
