using System;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class HttpBypassHelper
    {
        public static bool ShouldSkipResource(string requestUri, string[] patternsToSkip)
        {
            requestUri = requestUri.ToLower();
            for (var index = 0; index < patternsToSkip.Length; index++)
            {
                if (requestUri.Contains(patternsToSkip[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
