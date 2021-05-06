using System;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class HttpBypassHelper
    {
        public static bool ShouldSkipResource(Uri requestUri, string[] patternsToSkip)
        {
            if (patternsToSkip == null)
            {
                return false;
            }

            var uriString = requestUri.ToString().ToLower();
            for (var index = 0; index < patternsToSkip.Length; index++)
            {
                if (uriString.Contains(patternsToSkip[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
