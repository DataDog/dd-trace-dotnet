// <copyright file="UriExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETCOREAPP3_1_OR_GREATER

using System;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// Stub for OpenTelemetry's URI extension methods.
    /// Used by vendored gRPC client for endpoint path handling.
    /// </summary>
    internal static class UriExtensions
    {
        public static Uri AppendPathIfNotPresent(this Uri uri, string path)
        {
            var absoluteUri = uri.AbsoluteUri;
            if (absoluteUri.EndsWith(path, StringComparison.Ordinal))
            {
                return uri;
            }

            return new Uri(uri, path);
        }
    }
}
#endif
