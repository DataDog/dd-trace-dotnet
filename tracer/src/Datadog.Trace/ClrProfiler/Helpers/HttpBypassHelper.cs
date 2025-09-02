// <copyright file="HttpBypassHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class HttpBypassHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UriContainsAnyOf(Uri? requestUri, string[]? substrings)
        {
            if (requestUri == null || substrings == null || substrings.Length == 0)
            {
                // fast path which covers the most common case:
                // DD_TRACE_HTTP_CLIENT_EXCLUDED_URL_SUBSTRINGS is not set so substrings is an empty array
                return false;
            }

            return UriContainsAnyOfSlow(requestUri, substrings);
        }

        private static bool UriContainsAnyOfSlow(Uri requestUri, string[] substrings)
        {
#if NETCOREAPP3_1_OR_GREATER
            var uriString = requestUri.ToString();
            foreach (var substring in substrings)
            {
                if (uriString.Contains(substring, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
#else
            var uriString = requestUri.ToString().ToUpperInvariant();

            for (var index = 0; index < substrings.Length; index++)
            {
                if (uriString.Contains(substrings[index]))
                {
                    return true;
                }
            }
#endif
            return false;
        }
    }
}
