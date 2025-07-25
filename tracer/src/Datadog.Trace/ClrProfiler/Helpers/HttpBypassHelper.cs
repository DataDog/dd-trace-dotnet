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
        public static bool UriContainsAnyOf(Uri requestUri, string[] substrings)
        {
            return substrings.Length > 0 && UriContainsAnyOfSlow(requestUri, substrings);
        }

        private static bool UriContainsAnyOfSlow(Uri requestUri, string[] substrings)
        {
            var uriString = requestUri.ToString().ToUpperInvariant();

            for (var index = 0; index < substrings.Length; index++)
            {
                if (uriString.Contains(substrings[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
