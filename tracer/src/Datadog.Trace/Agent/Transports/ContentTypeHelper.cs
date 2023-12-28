// <copyright file="ContentTypeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Agent.Transports;

internal static class ContentTypeHelper
{
    public static string GetContentType(string contentType, string? multipartBoundary)
        => string.IsNullOrEmpty(multipartBoundary)
               ? contentType
               : $"{contentType}; boundary={multipartBoundary}";
}
