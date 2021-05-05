using System;

namespace Datadog.Trace.ClrProfiler.Helpers
{
    internal static class HttpBypassHelper
    {
        public static bool ShouldSkipResource(string requestUri, string[] patternsToSkip)
        {
            for (var y = 0; y < patternsToSkip.Length; y++)
            {
                var found = (requestUri.Length - requestUri.Replace(patternsToSkip[y], string.Empty).Length) / patternsToSkip[y].Length > 0;
                if (found)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
