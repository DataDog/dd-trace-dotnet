using System;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class HttpBypassHelper
    {
        public static bool ContainsAnyOf(this Uri requestUri, string[] substrings)
        {
            if (substrings == null || substrings.Length == 0)
            {
                return false;
            }

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
